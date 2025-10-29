$RG="rg-energy-advisor"
$APP="energy-advisor-web97487"
$ACR="energyadvisoracr78952"
$REPO="energy-advisor"
$APP_MI_OBJECT_ID=$(az webapp show -g $RG -n $APP --query identity.principalId -o tsv)
$ACR_ID=$(az acr show -n $ACR --query id -o tsv)

az webapp config container show -g $RG -n $APP -o json
az webapp show -g $RG -n $APP --query identity -o json
az role assignment list --assignee-object-id $APP_MI_OBJECT_ID --scope $ACR_ID -o table
az acr repository show-tags -n $ACR --repository $REPO -o table | Select-Object -Last 10