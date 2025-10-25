-- Purpose: Additive migration for Bell Center APIs (FISA) — schema v3.1b
-- Notes:
--  - Non-breaking, additive. Uses IF NOT EXISTS everywhere possible.
--  - Aligns with FISA_Bell_Center_APIs_v1.0.md.
--  - Assumes existing schema: public (default).
--  - Assumes existing tables: users(id UUID PK). If different, adjust FK targets accordingly.
--  - Outbox table is referenced by application; included here as a safe-guard stub IF NOT EXISTS (minimal columns).

BEGIN;

CREATE TABLE IF NOT EXISTS public.notifications (
    id                  uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    category            text,
    type                text,
    title               text NOT NULL,
    message             text,
    payload             jsonb,
    source_entity_type  text,
    source_entity_id    uuid,
    severity            text NOT NULL CHECK (severity IN ('info','warning','critical')),
    open_url            text,
    created_at          timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS public.user_notifications (
    id               uuid PRIMARY KEY DEFAULT uuid_generate_v4(),
    notification_id  uuid NOT NULL REFERENCES public.notifications(id) ON DELETE CASCADE,
    user_id          uuid NOT NULL REFERENCES public.users(id) ON DELETE CASCADE,
    is_read          boolean NOT NULL DEFAULT false,
    read_at          timestamptz,
    is_hidden        boolean NOT NULL DEFAULT false,
    created_at       timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_user_notifications_notification_user UNIQUE (notification_id, user_id)
);

CREATE TABLE IF NOT EXISTS public.outbox_events (
    id              bigserial PRIMARY KEY,
    aggregate_type  text NOT NULL,
    aggregate_id    uuid,
    event_type      text NOT NULL,
    payload         jsonb,
    created_at      timestamptz NOT NULL DEFAULT now(),
    processed_at    timestamptz NULL
);

CREATE INDEX IF NOT EXISTS idx_un_user_hidden_read_created
    ON public.user_notifications(user_id, is_hidden, is_read, created_at DESC);

CREATE INDEX IF NOT EXISTS idx_un_user_hidden_read_id
    ON public.user_notifications(user_id, is_hidden, is_read, id);

CREATE INDEX IF NOT EXISTS idx_n_created_cat_sev
    ON public.notifications(created_at DESC, category, severity);

CREATE INDEX IF NOT EXISTS idx_outbox_unprocessed
    ON public.outbox_events (processed_at NULLS FIRST, id ASC);

COMMENT ON TABLE public.notifications IS 'System-wide notifications (Bell Center)';
COMMENT ON COLUMN public.notifications.payload IS 'Compact JSON payload for rendering/navigation';
COMMENT ON TABLE public.user_notifications IS 'Per-user fan-out notifications with read/hide flags';

COMMIT;
