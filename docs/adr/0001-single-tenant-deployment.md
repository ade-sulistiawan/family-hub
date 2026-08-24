# Single-tenant deployment, no tenant isolation

Family Hub is deployed one instance per Household, not as multi-tenant SaaS serving many Households from one deployment. Chosen for simplicity now that this is a personal/family tool, not a product being sold to multiple customers. Revisit (adding a `HouseholdId` scope to every query) only if that changes.
