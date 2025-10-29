param(
  [Parameter(Mandatory=$true)] [string]$SubscriptionId,
  [Parameter(Mandatory=$true)] [string]$Location,
  # Infra RG (ACR, App Service, etc.)
  [Parameter(Mandatory=$true)] [string]$InfraRgName,
  # Remote state RG + Storage
  [Parameter(Mandatory=$true)] [string]$TfstateRgName,
  [Parameter(Mandatory=$true)] [string]$TfstateSaName,      # must be globally unique, lower-case
  [string]$TfstateContainerName = "tfstate",
  # Service Principal
  [string]$SpName = "gh-ci-tf",
  # GitHub wiring (optional). Format: owner/repo e.g. myorg/energy-advisor
  [string]$GitHubRepo = "",
  [switch]$SetGithubSecretsAndVars  # requires GitHub CLI 'gh' logged in
)

function Ensure-AzLogin {
  if (-not (az account show 2>$null)) {
    Write-Host "🔐 Azure login required..."
    az login | Out-Null
  }
  az account set --subscription $SubscriptionId
}

function Ensure-ResourceGroup {
  param([string]$Name, [string]$Loc)
  $exists = az group show -n $Name --query name -o tsv 2>$null
  if (-not $exists) {
    Write-Host "🛠 Creating RG '$Name' in $Loc..."
    az group create -n $Name -l $Loc | Out-Null
  } else {
    Write-Host "✅ RG '$Name' already exists."
  }
}

function Ensure-StorageAccount {
  param([string]$Rg, [string]$Sa, [string]$Loc)
  $exists = az storage account show -n $Sa -g $Rg --query name -o tsv 2>$null
  if (-not $exists) {
    Write-Host "🗄 Creating Storage Account '$Sa' in RG '$Rg'..."
    az storage account create -n $Sa -g $Rg -l $Loc --sku Standard_LRS --encryption-services blob | Out-Null
  } else {
    Write-Host "✅ Storage Account '$Sa' already exists."
  }
}

function Ensure-BlobContainer {
  param([string]$Sa, [string]$Container)
  $conn = az storage account show-connection-string -n $Sa --query connectionString -o tsv
  $exists = az storage container show --name $Container --account-name $Sa 2>$null
  if ($LASTEXITCODE -ne 0) {
    Write-Host "📦 Creating Blob Container '$Container'..."
    az storage container create --name $Container --account-name $Sa | Out-Null
  } else {
    Write-Host "✅ Blob container '$Container' already exists."
  }
}

function Ensure-ServicePrincipal {
  param([string]$SpDisplayName, [string]$Role, [string[]]$Scopes)
  $appId = az ad sp list --display-name $SpDisplayName --query "[0].appId" -o tsv 2>$null
  if (-not $appId) {
    Write-Host "🧩 Creating Service Principal '$SpDisplayName'..."
    $json = az ad sp create-for-rbac `
      --name $SpDisplayName `
      --role $Role `
      --scopes ($Scopes -join " ") `
      --sdk-auth
  } else {
    Write-Host "♻️  SP '$SpDisplayName' exists — resetting credentials..."
    $json = az ad sp credential reset `
      --name $appId `
      --credential-description "gh-ci-token" `
      --years 2 `
      --sdk-auth
  }
  return $json
}

# ---------- RUN ----------
Ensure-AzLogin

# Ensure RGs
Ensure-ResourceGroup -Name $InfraRgName   -Loc $Location
Ensure-ResourceGroup -Name $TfstateRgName -Loc $Location

# Ensure tfstate SA + container
Ensure-StorageAccount -Rg $TfstateRgName -Sa $TfstateSaName -Loc $Location
Ensure-BlobContainer  -Sa $TfstateSaName -Container $TfstateContainerName

# Create/Reset SP with Contributor scoped to BOTH RGs (least privilege)
$infraScope   = "/subscriptions/$SubscriptionId/resourceGroups/$InfraRgName"
$tfstateScope = "/subscriptions/$SubscriptionId/resourceGroups/$TfstateRgName"
$spJson = Ensure-ServicePrincipal -SpDisplayName $SpName -Role "Contributor" -Scopes @($infraScope,$tfstateScope)

Write-Host ""
Write-Host "✅ Azure bootstrap complete."
Write-Host "➡️  Terraform backend:"
Write-Host "   resource_group_name = $TfstateRgName"
Write-Host "   storage_account_name = $TfstateSaName"
Write-Host "   container_name = $TfstateContainerName"
Write-Host "   key = energy-advisor.tfstate"
Write-Host ""

# Optionally set GitHub Secrets/Vars
if ($SetGithubSecretsAndVars -and $GitHubRepo) {
  if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    Write-Warning "GitHub CLI 'gh' not found; skipping secrets/vars."
  } else {
    Write-Host "🔗 Pushing secrets & variables to GitHub repo $GitHubRepo"
    # Secrets
    $null = $spJson | gh secret set AZURE_CREDENTIALS -R $GitHubRepo -b -
    gh secret set AZ_SUBSCRIPTION_ID -R $GitHubRepo -b "$SubscriptionId"
    $tenantId = az account show --query tenantId -o tsv
    gh secret set AZ_TENANT_ID -R $GitHubRepo -b "$tenantId"
    # Variables (for workflows)
    gh variable set TFSTATE_RG        -R $GitHubRepo -b "$TfstateRgName"
    gh variable set TFSTATE_SA        -R $GitHubRepo -b "$TfstateSaName"
    gh variable set TFSTATE_CONTAINER -R $GitHubRepo -b "$TfstateContainerName"
    gh variable set TFSTATE_KEY       -R $GitHubRepo -b "azure-observability.tfstate"
    gh variable set INFRA_RG          -R $GitHubRepo -b "$InfraRgName"
    gh variable set LOCATION          -R $GitHubRepo -b "$Location"
    Write-Host "✅ GitHub secrets/vars updated."
  }
} else {
  Write-Host "ℹ️  Skipping GitHub secrets/vars. Provide -SetGithubSecretsAndVars and -GitHubRepo 'owner/repo' to auto-publish."
}

Write-Host ""
Write-Host "📤 Paste the following JSON into GitHub Secret 'AZURE_CREDENTIALS' if you didn't auto-publish:"
Write-Output $spJson
