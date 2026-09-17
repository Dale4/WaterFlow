param(
    [ValidateSet("dev", "test")]
    [string]$Env = "dev",

    [string]$Region = "us-west-1",

    [string]$ContainerName = "Api",

    [string]$EcrRepository = "waterflow/dataservices",

    [string]$ImageTag = "latest"
)

$ErrorActionPreference = "Stop"

function ConvertTo-PlainObject {
    param($Value)

    if ($null -eq $Value) {
        return $null
    }

    if ($Value -is [string] -or $Value -is [bool] -or $Value -is [decimal] -or
        $Value -is [double] -or $Value -is [int] -or $Value -is [long] -or $Value -is [uint32]) {
        return $Value
    }

    if ($Value -is [datetime]) {
        return $Value.ToString("o")
    }

    if ($Value -is [System.Collections.IDictionary]) {
        $map = @{}
        foreach ($key in $Value.Keys) {
            $converted = ConvertTo-PlainObject $Value[$key]
            if ($null -ne $converted) {
                $map[$key] = $converted
            }
        }
        return $map
    }

    if ($Value -is [System.Collections.IEnumerable]) {
        $list = New-Object System.Collections.ArrayList
        foreach ($item in $Value) {
            [void]$list.Add((ConvertTo-PlainObject $item))
        }
        return $list
    }

    if ($Value -is [psobject]) {
        $map = @{}
        foreach ($property in $Value.PSObject.Properties) {
            $converted = ConvertTo-PlainObject $property.Value
            if ($null -ne $converted) {
                $map[$property.Name] = $converted
            }
        }
        return $map
    }

    return $Value
}

function ConvertTo-EcsJson {
    param($TaskDefinition)

    $plain = ConvertTo-PlainObject $TaskDefinition
    foreach ($name in @("containerDefinitions", "requiresCompatibilities", "volumes", "placementConstraints")) {
        if ($plain.ContainsKey($name) -and $plain[$name] -isnot [System.Collections.IList]) {
            $plain[$name] = New-Object System.Collections.ArrayList (, $plain[$name])
        }
    }

    foreach ($container in @($plain["containerDefinitions"])) {
        if ($container -isnot [System.Collections.IDictionary]) {
            continue
        }
        foreach ($name in @("portMappings", "environment", "secrets", "mountPoints", "volumesFrom")) {
            if ($container.ContainsKey($name) -and $container[$name] -isnot [System.Collections.IList]) {
                $container[$name] = New-Object System.Collections.ArrayList (, $container[$name])
            }
        }
    }

    Add-Type -AssemblyName System.Web.Extensions
    $serializer = New-Object System.Web.Script.Serialization.JavaScriptSerializer
    $serializer.MaxJsonLength = [int]::MaxValue
    return $serializer.Serialize($plain)
}

$cluster = "app-platform-$Env"
$service = "app-platform-$Env-waterflow"

Write-Host "Deploying $EcrRepository:$ImageTag to $cluster / $service ($Region)"

$account = aws sts get-caller-identity --query Account --output text
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($account)) {
    throw "Could not read AWS account. Configure AWS CLI credentials first."
}

$image = "$account.dkr.ecr.$Region.amazonaws.com/${EcrRepository}:$ImageTag"

$taskDefArn = aws ecs describe-services `
    --region $Region `
    --cluster $cluster `
    --services $service `
    --query "services[0].taskDefinition" `
    --output text
if ($LASTEXITCODE -ne 0 -or $taskDefArn -eq "None" -or [string]::IsNullOrWhiteSpace($taskDefArn)) {
    throw "Service $service was not found on cluster $cluster. Create the env first (app-platform deploy-env.ps1)."
}

$td = aws ecs describe-task-definition `
    --region $Region `
    --task-definition $taskDefArn `
    --query taskDefinition `
    --output json | ConvertFrom-Json

$container = @($td.containerDefinitions) | Where-Object { $_.name -eq $ContainerName }
if (-not $container) {
    throw "Container '$ContainerName' was not found in $taskDefArn."
}

$container.image = $image

foreach ($name in @(
        "taskDefinitionArn",
        "revision",
        "status",
        "requiresAttributes",
        "compatibilities",
        "registeredAt",
        "registeredBy",
        "deregisteredAt"
    )) {
    $td.PSObject.Properties.Remove($name)
}

$executionRoleArn = $td.executionRoleArn
if ($executionRoleArn) {
    $roleName = $executionRoleArn.Split("/")[-1]
    Write-Host "Ensuring $roleName can pull from ECR"
    aws iam attach-role-policy `
        --role-name $roleName `
        --policy-arn "arn:aws:iam::aws:policy/service-role/AmazonECSTaskExecutionRolePolicy"
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to attach AmazonECSTaskExecutionRolePolicy to $roleName."
    }
}

$utf8 = New-Object System.Text.UTF8Encoding $false
$tmp = Join-Path ([System.IO.Path]::GetTempPath()) "waterflow-task-def.json"
[System.IO.File]::WriteAllText($tmp, (ConvertTo-EcsJson $td), $utf8)

try {
    $newArn = aws ecs register-task-definition `
        --region $Region `
        --cli-input-json "file://$tmp" `
        --query "taskDefinition.taskDefinitionArn" `
        --output text
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($newArn)) {
        throw "Failed to register a new task definition."
    }
}
finally {
    Remove-Item -LiteralPath $tmp -ErrorAction SilentlyContinue
}

Write-Host "Registered $newArn"
Write-Host "Updating service to $image"

aws ecs update-service `
    --region $Region `
    --cluster $cluster `
    --service $service `
    --task-definition $newArn `
    --force-new-deployment `
    --query "service.{status:status,taskDefinition:taskDefinition}" `
    --output table
if ($LASTEXITCODE -ne 0) {
    throw "Failed to update service $service."
}

Write-Host "Waiting for service to stabilize..."
aws ecs wait services-stable --region $Region --cluster $cluster --services $service
if ($LASTEXITCODE -ne 0) {
    throw "Service $service did not become stable. Check ECS events and that $image exists."
}

Write-Host "Done. $service is running $image"
