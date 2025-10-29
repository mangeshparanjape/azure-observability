$SUB_ID='0203993f-61a4-4de8-843b-df45555ee5fd'
$CI_APP_ID='f80fa9fb-0097-4a35-a54b-d17e57b50f3e'
az role assignment create `
--assignee $CI_APP_ID `
--role "User Access Administrator" `
--scope "/subscriptions/$SUB_ID/resourceGroups/rg-azure-observability"