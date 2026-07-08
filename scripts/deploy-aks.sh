#!/usr/bin/env bash
#
# Log in to ACR, build & push the PlannerPro images, and update the AKS
# deployments in the brr-dev-ingress namespace.
#
#   1. az acr login  -> buddynetworks
#   2. docker build + push  plannerproapi  and  plannerproweb
#   3. kubectl apply the manifests (idempotent) and roll the deployments to the
#      freshly-pushed tag.
#
# Prereqs: logged in with `az login`, and kubectl context set to the AKS cluster
# (`az aks get-credentials -g <rg> -n <cluster>`). The plannerpro-secrets Secret
# and the buddynetworks37436e08-auth imagePullSecret must already exist in the
# namespace.
#
# Usage:
#   ./scripts/deploy-aks.sh                 # timestamp tag
#   TAG=v1.4.0 ./scripts/deploy-aks.sh      # explicit tag
#   SKIP_APPLY=1 ./scripts/deploy-aks.sh    # build/push + set image only
#
# Env overrides: ACR, NAMESPACE, TAG, SKIP_APPLY

set -euo pipefail

ACR="${ACR:-buddynetworks}"
NAMESPACE="${NAMESPACE:-brr-dev-ingress}"
TAG="${TAG:-$(date +%Y%m%d%H%M%S)}"
SKIP_APPLY="${SKIP_APPLY:-0}"

REGISTRY="${ACR}.azurecr.io"
API_IMAGE="${REGISTRY}/plannerproapi"
WEB_IMAGE="${REGISTRY}/plannerproweb"
# Repo root is the parent of this scripts/ folder.
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

echo "==> PlannerPro -> AKS  (registry=${REGISTRY}  ns=${NAMESPACE}  tag=${TAG})"

# --- 1. Log in to ACR (also wires docker up for push) -------------------------
echo "==> az acr login --name ${ACR}"
az acr login --name "${ACR}"

# --- 2. Build & push both images ----------------------------------------------
echo "==> Build + push API image"
docker build -t "${API_IMAGE}:${TAG}" -t "${API_IMAGE}:latest" -f "${ROOT}/PlannerPro.Api/Dockerfile" "${ROOT}"
docker push "${API_IMAGE}:${TAG}"
docker push "${API_IMAGE}:latest"

echo "==> Build + push Web image"
docker build -t "${WEB_IMAGE}:${TAG}" -t "${WEB_IMAGE}:latest" -f "${ROOT}/PlannerPro.Web/Dockerfile" "${ROOT}/PlannerPro.Web"
docker push "${WEB_IMAGE}:${TAG}"
docker push "${WEB_IMAGE}:latest"

# --- 3. Apply manifests (idempotent; safe to re-run) --------------------------
if [[ "${SKIP_APPLY}" != "1" ]]; then
  echo "==> kubectl apply manifests"
  kubectl apply -n "${NAMESPACE}" -f "${ROOT}/k8s/api-service.yml"
  kubectl apply -n "${NAMESPACE}" -f "${ROOT}/k8s/api-deployment.yml"
  kubectl apply -n "${NAMESPACE}" -f "${ROOT}/k8s/web-service.yml"
  kubectl apply -n "${NAMESPACE}" -f "${ROOT}/k8s/web-deployment.yml"
fi

# --- 4. Update AKS to the freshly-pushed tag & wait ---------------------------
echo "==> Rolling deployments to ${TAG}"
kubectl set image deployment/plannerproapi plannerproapi="${API_IMAGE}:${TAG}" -n "${NAMESPACE}"
kubectl set image deployment/plannerproweb plannerproweb="${WEB_IMAGE}:${TAG}" -n "${NAMESPACE}"

kubectl rollout status deployment/plannerproapi -n "${NAMESPACE}" --timeout=180s
kubectl rollout status deployment/plannerproweb -n "${NAMESPACE}" --timeout=120s

echo "==> Done. Deployed ${TAG} to ${NAMESPACE}."
