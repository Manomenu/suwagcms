-- Why: no foreign key, since the entries of a deleted resource stay readable; and no backfill, since an entry is never changed, so one written earlier stays found under the id it was filed under.
ALTER TABLE change_entry
    ADD COLUMN resource_id uuid;
