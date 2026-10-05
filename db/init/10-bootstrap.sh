#!/usr/bin/env bash
set -euo pipefail

: "${INITIAL_ADMIN_USERNAME:?Set INITIAL_ADMIN_USERNAME in db/.env}"
: "${INITIAL_ADMIN_NORMALIZED_USERNAME:?Generate the normalized Identity username and set it in db/.env}"
: "${INITIAL_ADMIN_PASSWORD_HASH:?Generate an Identity password hash and set INITIAL_ADMIN_PASSWORD_HASH in db/.env}"
: "${APP_DB_USER:?Set APP_DB_USER in db/.env}"
: "${APP_DB_PASSWORD:?Set APP_DB_PASSWORD in db/.env}"

psql --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" --set ON_ERROR_STOP=1 \
  --file /bootstrap/001_mvp_schema.sql

psql --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" --set ON_ERROR_STOP=1 \
  --file /bootstrap/002_catalog_seed.sql

psql --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" --set ON_ERROR_STOP=1 \
  --set "admin_username=$INITIAL_ADMIN_USERNAME" \
  --set "admin_normalized_username=$INITIAL_ADMIN_NORMALIZED_USERNAME" \
  --set "admin_password_hash=$INITIAL_ADMIN_PASSWORD_HASH" \
  --file /bootstrap/seed-inicial.sql

psql --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" --set ON_ERROR_STOP=1 \
  --set "app_db_user=$APP_DB_USER" \
  --set "app_db_password=$APP_DB_PASSWORD" \
  --file /bootstrap/003_runtime_role_seed.sql
