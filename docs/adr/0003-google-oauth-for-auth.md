# Authenticate via Google OAuth, not local accounts

Family Members sign in with "Sign in with Google" rather than a locally-managed username/password (ASP.NET Core Identity local accounts). Chosen to avoid owning password storage, reset flows, and credential security for a small household, at the cost of requiring every Family Member to have a Google account.
