# Organize the codebase as a vertical slice per tracker

Each tracker (Chore Tracker, Expiry Tracker, Warranty & Receipt Archiver, Medication & Dosage Reminders, First-Aid Kit Inventory, and future ones) owns its own self-contained module: entities, API endpoints, and Blazor pages together, rather than sharing layered `Controllers/`/`Models/`/`Pages/` folders across trackers. Chosen because more trackers are explicitly planned, and this keeps adding one to "add a folder" instead of touching every shared layer.
