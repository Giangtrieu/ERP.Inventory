# CURRENT STATUS

## Fixed

### Location rollback

CurrentItemLocation restoration mostly works.

### Quantity lifecycle

Partial fixes implemented:

- quantity rollback
- quantity rebuild
- duplicate replay prevention

Needs verification.

---

## Known Issues

### Borrow lifecycle

Scenario:

Borrow
Return
Borrow
Return
Borrow

Deleting transactions sequentially:

Location:
OK

Status:
Incorrect

Expected:

Available
LentOut
Returned
LentOut
Returned

Actual:

Location restored
Status remains LentOut

Need audit.

---

### Repair lifecycle

Not fully tested.

Need audit.

Determine whether current schema can reconstruct:

- location
- status

---

### QuantityOnly

Business change required:

OLD:
SN based

NEW:
PN + Quantity

No ItemInstance creation.

---

### Dashboard

Need:

Chart 1:
Quantity by ItemCode

Chart 2:
Quantity by ItemCategory

---

### LogErrorSystem

Paused feature.

Needs:

- persistence
- UI
- localization
- super admin protection
