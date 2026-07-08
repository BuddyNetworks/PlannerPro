<#
.SYNOPSIS
  Build, push, and deploy PlannerPro (API + Web) to AKS.

.DESCRIPTION
  Builds the two container images, pushes them to the buddynetworks ACR, and
  rolls them out to the brr-dev-ingress namespace. Assumes you are already
  logged in to Azure (az login) and your kubectl context points at the AKS
  cluster (az aks get-credentials ...).

.EXAMPLE
  ./deploy.ps1                       # build + push + deploy with a timestamp tag
  ./deploy.ps1 -Tag v1.2.3           # use an explicit image tag
  ./deploy.ps1 -SkipBuild            # just re-apply manifests + roll to -Tag
  ./deploy.ps1 -CreatePullSecret     # (re)create the ACR imagePullSecret first
#>
[CmdletBinding()]
param(
  [string]$Acr = "buddynetworks",
  [string]$Namespace = "brr-dev-ingress",
  [string]$Tag = (Get-Date -Format "yyyyMMddHHmmss"),
  [switch]$SkipBuild,
  [switch]$CreatePullSecret
)

$ErrorActionPreference = "Stop"
$Registry   = "$Acr.azurecr.io"
$ApiImage   = "$Registry/plannerproapi"
$WebImage   = "$Registry/plannerproweb"
$Root       = $PSScriptRoot

Write-Host "==> PlannerPro deploy  (registry=$Registry  ns=$Namespace  tag=$Tag)" -ForegroundColor Cyan

# --- 1. Authenticate to ACR (also configures docker for push) -----------------
az acr login --name $Acr

# --- 2. (optional) create the imagePullSecret the deployments reference --------
if ($CreatePullSecret) {
  Write-Host "==> Creating imagePullSecret buddynetworks37436e08-auth" -ForegroundColor Cyan
  $acrUser = az acr credential show -n $Acr --query "username" -o tsv
  $acrPass = az acr credential show -n $Acr --query "passwords[0].value" -o tsv
  kubectl create secret docker-registry buddynetworks37436e08-auth `
    --docker-server=$Registry `
    --docker-username=$acrUser `
    --docker-password=$acrPass `
    -n $Namespace --dry-run=client -o yaml | kubectl apply -f -
}

# --- 3. Build & push both images ----------------------------------------------
if (-not $SkipBuild) {
  Write-Host "==> Building API image (context = repo root)" -ForegroundColor Cyan
  docker build -t "${ApiImage}:$Tag" -t "${ApiImage}:latest" -f "$Root/PlannerPro.Api/Dockerfile" $Root
  docker push "${ApiImage}:$Tag"
  docker push "${ApiImage}:latest"

  Write-Host "==> Building Web image (context = PlannerPro.Web)" -ForegroundColor Cyan
  docker build -t "${WebImage}:$Tag" -t "${WebImage}:latest" -f "$Root/PlannerPro.Web/Dockerfile" "$Root/PlannerPro.Web"
  docker push "${WebImage}:$Tag"
  docker push "${WebImage}:latest"
}

# --- 4. Apply manifests (idempotent) ------------------------------------------
# NOTE: apply the plannerpro-secrets Secret once, out of band (see
# k8s/plannerpro-secrets.example.yml). It is intentionally not applied here.
Write-Host "==> Applying manifests" -ForegroundColor Cyan
kubectl apply -n $Namespace -f "$Root/k8s/api-service.yml"
kubectl apply -n $Namespace -f "$Root/k8s/api-deployment.yml"
kubectl apply -n $Namespace -f "$Root/k8s/web-service.yml"
kubectl apply -n $Namespace -f "$Root/k8s/web-deployment.yml"
kubectl apply -n $Namespace -f "$Root/k8s/ingress.yml"

# --- 5. Pin the freshly-pushed tag & wait for rollout -------------------------
Write-Host "==> Rolling out $Tag" -ForegroundColor Cyan
kubectl set image deployment/plannerproapi plannerproapi="${ApiImage}:$Tag" -n $Namespace
kubectl set image deployment/plannerproweb plannerproweb="${WebImage}:$Tag" -n $Namespace

kubectl rollout status deployment/plannerproapi -n $Namespace --timeout=180s
kubectl rollout status deployment/plannerproweb -n $Namespace --timeout=120s

Write-Host "==> Done. Deployed tag $Tag to $Namespace." -ForegroundColor Green
