# Family Hub

A household management PWA: trackers for chores, expiring items, warranties/receipts, medication, and first-aid stock, shared across the people in one household.

## Language

**Household**:
The family unit that owns all data in one Family Hub tenant — its members, items, chores, and reminders.
_Avoid_: Family, account, tenant

**Family Member**:
An individual person with their own sign-in, belonging to exactly one Household. Chores and medication reminders are assigned to a Family Member, not to the Household as a whole.
_Avoid_: User, member, person

**Item**:
The shared physical-thing concept underlying Expiry Tracker, Warranty & Receipt Archiver, and First-Aid Kit Inventory; each tracker attaches its own Facet of data to an Item. Surfaced to users only through each tracker's own menu, never as a combined generic list.
_Avoid_: Household Item, Possession, Asset

**Facet**:
A single tracker's slice of data attached to an Item — an Item can carry more than one Facet at once (a first-aid bandage box has both an Expiry Facet and a Stock Facet). A Facet belongs to exactly one tracker; it never holds another tracker's concerns.
_Avoid_: Attribute, Property

**Expiry Facet**:
The "goes bad on date X" data Expiry Tracker attaches to an Item — covers perishable/consumable goods, including a medication bottle's own printed expiration date. Distinct from a Medication Reminder's dosage schedule, which is about when to take a dose, not when the bottle expires.
_Avoid_: Expiration date, Best-by date

**Warranty Facet**:
The purchase/warranty/receipt data (via a Document Reference) Warranty & Receipt Archiver attaches to an Item. Never holds an expiry-style "goes bad" date — that's the Expiry Facet's job even if the two happen to co-occur on the same Item.
_Avoid_: Warranty info, Receipt data

**Stock Facet**:
The quantity-on-hand and low-stock-threshold data First-Aid Kit Inventory attaches to an Item, alongside that Item's Expiry Facet.
_Avoid_: Inventory count, Stock level

**Document Reference**:
A pointer (external document ID) an Item's Warranty & Receipt facet holds into the Household's Paperless-ngx instance. Family Hub stores the reference and fetches the file via Paperless-ngx's API when needed; it never stores the file bytes itself.
_Avoid_: Attachment, File, Upload

**Chore Occurrence**:
A single dated instance of a Chore (today's dishes, this Tuesday's trash), the unit that gets marked done. A recurring Chore generates one Chore Occurrence per scheduled date; a one-off Chore has exactly one.
_Avoid_: Task, Chore instance

**Dose Log**:
A record that a specific Family Member's medication dose was taken, skipped, or missed at a scheduled time. The history of Dose Logs is what makes a Medication Reminder trustworthy, not just an alarm.
_Avoid_: Dose record, Medication history

**Scheduled Medication**:
A medication with a fixed dosage schedule (specific times each day) that automatically generates reminders and expected Dose Log entries.
_Avoid_: Recurring medication

**PRN Medication**:
An as-needed medication with no fixed schedule and no generated reminder; a Family Member logs a Dose Log entry on demand when taken, so spacing between doses is still visible in history.
_Avoid_: As-needed medication (fine in conversation, but use PRN Medication in code/docs)

**Lead Time**:
A per-Item, configurable setting on the Expiry Facet controlling how far before the expiry date a notification fires, defaulting to a sensible value so an Item doesn't require configuration to be useful.
_Avoid_: Notification window, Alert threshold

**Join Code**:
The code (or invite link) an existing Household shows so a new Family Member can join it. The first Family Member to sign in to a not-yet-existing Household creates it; everyone after uses a Join Code rather than any separate admin action.
_Avoid_: Invite code, Household code
