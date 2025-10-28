# Azure Observability (APIM + Function App) via Terraform & GitHub Actions

This repo deploys a full, testable monitoring stack:
- API Management (Developer SKU)
- Azure Function App (Linux, Consumption)
- Log Analytics Workspace + Application Insights (workspace-based)
- Diagnostic Settings wired to LAW
- Action Group (email)
- Metric Alerts (APIM & Function App, with correct metric names/dimensions)
- Alert Processing Rule to route alerts
- GitHub Actions pipeline using OIDC to Azure

## Usage
1) Create a Federated **Service Principal** for GitHub OIDC and grant **Contributor** on your subscription.
2) Add repo secrets:
   - `AZURE_TENANT_ID`
   - `AZURE_SUBSCRIPTION_ID`
   - `AZURE_CLIENT_ID`
3) Edit `envs/dev/terraform.tfvars` (at minimum set `email_receiver`, publisher info).
4) Push to `main` to run the pipeline (or run Terraform CLI locally).

## Local CLI
```bash
cd terraform
terraform init
terraform apply -auto-approve -var-file="../envs/dev/terraform.tfvars"
```

## Triggering a test alert
- For APIM: call a backend that returns 500s or temporarily drop the 5xx threshold.
- For Function App: add a sample HTTP-trigger that returns 500 and hit it a few times.
