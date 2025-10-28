
locals {
  rg_name   = "${var.name_prefix}-rg-${var.env}"
  law_name  = "${var.name_prefix}-law-${var.env}"
  ai_name   = "${var.name_prefix}-ai-${var.env}"
  apim_name = "${var.name_prefix}-apim-${var.env}"
  st_name   = replace("${var.name_prefix}st${var.env}", "-", "")
  plan_name = "${var.name_prefix}-plan-${var.env}"
  func_name = "${var.name_prefix}-func-${var.env}"
  ag_name   = "${var.name_prefix}-ag-${var.env}"
}

# -------- Resource Group --------
resource "azurerm_resource_group" "rg" {
  name     = local.rg_name
  location = var.location
}

# -------- Log Analytics Workspace --------
resource "azurerm_log_analytics_workspace" "law" {
  name                = local.law_name
  resource_group_name = azurerm_resource_group.rg.name
  location            = var.location
  sku                 = "PerGB2018"
  retention_in_days   = 30
}

# -------- Application Insights (workspace-based) --------
resource "azurerm_application_insights" "ai" {
  name                = local.ai_name
  location            = var.location
  resource_group_name = azurerm_resource_group.rg.name
  workspace_id        = azurerm_log_analytics_workspace.law.id
  application_type    = "web"
}

# -------- API Management (Developer) --------
resource "azurerm_api_management" "apim" {
  name                = local.apim_name
  location            = var.location
  resource_group_name = azurerm_resource_group.rg.name

  publisher_name      = var.apim_publisher_name
  publisher_email     = var.apim_publisher_email

  sku_name            = "Developer_1"
}

# Diagnostic settings → LAW (APIM)
resource "azurerm_monitor_diagnostic_setting" "apim_diag" {
  name                       = "apim-to-law"
  target_resource_id         = azurerm_api_management.apim.id
  log_analytics_workspace_id = azurerm_log_analytics_workspace.law.id

  enabled_log {
    category_group = "allLogs"
  }
  metric {
    category = "AllMetrics"
    enabled  = true
  }
}

# -------- Function App (Linux Consumption) --------
resource "azurerm_storage_account" "st" {
  name                             = substr(local.st_name, 0, 24)
  resource_group_name              = azurerm_resource_group.rg.name
  location                         = var.location
  account_tier                     = "Standard"
  account_replication_type         = "LRS"
  allow_nested_items_to_be_public  = false
}

resource "azurerm_service_plan" "plan" {
  name                = local.plan_name
  location            = var.location
  resource_group_name = azurerm_resource_group.rg.name
  os_type             = "Linux"
  sku_name            = "Y1" # Consumption
}

resource "azurerm_linux_function_app" "func" {
  name                       = local.func_name
  location                   = var.location
  resource_group_name        = azurerm_resource_group.rg.name
  service_plan_id            = azurerm_service_plan.plan.id
  storage_account_name       = azurerm_storage_account.st.name
  storage_account_access_key = azurerm_storage_account.st.primary_access_key
  functions_extension_version = "~4"

  app_settings = {
    "APPLICATIONINSIGHTS_CONNECTION_STRING" = azurerm_application_insights.ai.connection_string
    "WEBSITE_RUN_FROM_PACKAGE"              = "1"
  }

  site_config {
    application_stack {
      use_custom_runtime = true
    }
  }
}

# Diagnostic settings → LAW (Function App)
resource "azurerm_monitor_diagnostic_setting" "func_diag" {
  name                       = "func-to-law"
  target_resource_id         = azurerm_linux_function_app.func.id
  log_analytics_workspace_id = azurerm_log_analytics_workspace.law.id

  enabled_log { category_group = "allLogs" }
  metric      { category = "AllMetrics" enabled = true }
}

# -------- Action Group (email only for now) --------
resource "azurerm_monitor_action_group" "ag" {
  name                = local.ag_name
  resource_group_name = azurerm_resource_group.rg.name
  short_name          = "ops"

  email_receiver {
    name          = "oncall"
    email_address = var.email_receiver
  }
}

# -------- Metric Alerts: APIM (correct metric names) --------

# Capacity — Sev1
resource "azurerm_monitor_metric_alert" "apim_capacity" {
  name                = "apim-capacity-high"
  resource_group_name = azurerm_resource_group.rg.name
  scopes              = [azurerm_api_management.apim.id]
  severity            = 1
  frequency           = "PT1M"
  window_size         = "PT5M"
  description         = "APIM Capacity > 75%"
  auto_mitigate       = true

  criteria {
    metric_namespace = "Microsoft.ApiManagement/service"
    metric_name      = "Capacity"
    aggregation      = "Average"
    operator         = "GreaterThan"
    threshold        = 75
  }
  action { action_group_id = azurerm_monitor_action_group.ag.id }
}

# CPU — Sev1 (CpuPercent_Gateway)
resource "azurerm_monitor_metric_alert" "apim_cpu" {
  name                = "apim-cpu-high"
  resource_group_name = azurerm_resource_group.rg.name
  scopes              = [azurerm_api_management.apim.id]
  severity            = 1
  frequency           = "PT1M"
  window_size         = "PT5M"
  description         = "APIM CPU > 80%"
  auto_mitigate       = true

  criteria {
    metric_namespace = "Microsoft.ApiManagement/service"
    metric_name      = "CpuPercent_Gateway"
    aggregation      = "Average"
    operator         = "GreaterThan"
    threshold        = 80
  }
  action { action_group_id = azurerm_monitor_action_group.ag.id }
}

# Memory — Sev1 (MemoryPercent_Gateway)
resource "azurerm_monitor_metric_alert" "apim_memory" {
  name                = "apim-memory-high"
  resource_group_name = azurerm_resource_group.rg.name
  scopes              = [azurerm_api_management.apim.id]
  severity            = 1
  frequency           = "PT1M"
  window_size         = "PT5M"
  description         = "APIM Memory > 80%"
  auto_mitigate       = true

  criteria {
    metric_namespace = "Microsoft.ApiManagement/service"
    metric_name      = "MemoryPercent_Gateway"
    aggregation      = "Average"
    operator         = "GreaterThan"
    threshold        = 80
  }
  action { action_group_id = azurerm_monitor_action_group.ag.id }
}

# Overall latency — Sev2 (Duration in ms)
resource "azurerm_monitor_metric_alert" "apim_latency" {
  name                = "apim-latency"
  resource_group_name = azurerm_resource_group.rg.name
  scopes              = [azurerm_api_management.apim.id]
  severity            = 2
  frequency           = "PT5M"
  window_size         = "PT10M"
  description         = "APIM Duration > 2000 ms"
  auto_mitigate       = true

  criteria {
    metric_namespace = "Microsoft.ApiManagement/service"
    metric_name      = "Duration"
    aggregation      = "Average"
    operator         = "GreaterThan"
    threshold        = 2000
  }
  action { action_group_id = azurerm_monitor_action_group.ag.id }
}

# Backend latency — Sev2 (BackendDuration in ms)
resource "azurerm_monitor_metric_alert" "apim_backend_latency" {
  name                = "apim-backend-latency"
  resource_group_name = azurerm_resource_group.rg.name
  scopes              = [azurerm_api_management.apim.id]
  severity            = 2
  frequency           = "PT5M"
  window_size         = "PT10M"
  description         = "APIM BackendDuration > 2000 ms"
  auto_mitigate       = true

  criteria {
    metric_namespace = "Microsoft.ApiManagement/service"
    metric_name      = "BackendDuration"
    aggregation      = "Average"
    operator         = "GreaterThan"
    threshold        = 2000
  }
  action { action_group_id = azurerm_monitor_action_group.ag.id }
}

# 5xx errors — Sev1 (Requests metric with dimension GatewayResponseCodeCategory=5xx)
resource "azurerm_monitor_metric_alert" "apim_5xx" {
  name                = "apim-5xx"
  resource_group_name = azurerm_resource_group.rg.name
  scopes              = [azurerm_api_management.apim.id]
  severity            = 1
  frequency           = "PT1M"
  window_size         = "PT5M"
  description         = "APIM 5xx errors > 50 per 5m"
  auto_mitigate       = true

  criteria {
    metric_namespace = "Microsoft.ApiManagement/service"
    metric_name      = "Requests"
    aggregation      = "Total"
    operator         = "GreaterThan"
    threshold        = 50

    dimension {
      name     = "GatewayResponseCodeCategory"
      operator = "Include"
      values   = ["5xx"]
    }
  }
  action { action_group_id = azurerm_monitor_action_group.ag.id }
}

# -------- Function App metric alerts --------

# HTTP 5xx — Sev2
resource "azurerm_monitor_metric_alert" "func_http5xx" {
  name                = "func-http5xx"
  resource_group_name = azurerm_resource_group.rg.name
  scopes              = [azurerm_linux_function_app.func.id]
  severity            = 2
  frequency           = "PT5M"
  window_size         = "PT10M"
  description         = "Function App Http5xx > 20 per 10m"
  auto_mitigate       = true

  criteria {
    metric_namespace = "Microsoft.Web/sites"
    metric_name      = "Http5xx"
    aggregation      = "Total"
    operator         = "GreaterThan"
    threshold        = 20
  }
  action { action_group_id = azurerm_monitor_action_group.ag.id }
}

# Response time — Sev2 (HttpResponseTime in seconds)
resource "azurerm_monitor_metric_alert" "func_latency" {
  name                = "func-latency"
  resource_group_name = azurerm_resource_group.rg.name
  scopes              = [azurerm_linux_function_app.func.id]
  severity            = 2
  frequency           = "PT5M"
  window_size         = "PT10M"
  description         = "Function App HttpResponseTime > 2s"
  auto_mitigate       = true

  criteria {
    metric_namespace = "Microsoft.Web/sites"
    metric_name      = "HttpResponseTime"
    aggregation      = "Average"
    operator         = "GreaterThan"
    threshold        = 2
  }
  action { action_group_id = azurerm_monitor_action_group.ag.id }
}

# Memory Working Set — Sev2 (bytes)
resource "azurerm_monitor_metric_alert" "func_mem" {
  name                = "func-memory-working-set"
  resource_group_name = azurerm_resource_group.rg.name
  scopes              = [azurerm_linux_function_app.func.id]
  severity            = 2
  frequency           = "PT5M"
  window_size         = "PT10M"
  description         = "Function App MemoryWorkingSet > 2GiB"
  auto_mitigate       = true

  criteria {
    metric_namespace = "Microsoft.Web/sites"
    metric_name      = "MemoryWorkingSet"
    aggregation      = "Average"
    operator         = "GreaterThan"
    threshold        = 2147483648
  }
  action { action_group_id = azurerm_monitor_action_group.ag.id }
}

# -------- Alert Processing Rule: route everything in RG to AG --------
resource "azurerm_monitor_alert_processing_rule_action" "apr_route" {
  name                = "apr-route-all"
  resource_group_name = azurerm_resource_group.rg.name
  location            = var.location
  enabled             = true
  scopes              = [azurerm_resource_group.rg.id]

  actions {
    action_group_id = azurerm_monitor_action_group.ag.id
  }
}
