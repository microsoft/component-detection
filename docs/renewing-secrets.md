# Renewing secrets

Almost all of our [workflows](../.github/workflows) require secrets and those secrets can be invalidated, deleted or expired so we need to know how to renew them.

The secrets in use today in the BCDE repo can be found [here](https://github.com/microsoft/componentdetection-bcde/settings/secrets):

* GH_PRIVATE_REPO_PAT

## Before Starting

Verify your account has sufficient permissions. You can do this by building BCDE locally. This will verify you have access to read the packages being created and read the private BCDE repo.

## Renewing GH_PRIVATE_REPO_PAT

1. Click this link: https://github.com/settings/tokens/new
1. (Optional) Name the token BCDE_GH_PRIVATE_REPO_PAT. This will make things easier to track in the future
1. Check the following permissions:
    * Full `repo` scope
    * `read:packages` scope
1. Click **Generate token** 
1. Copy and paste that token into notepad once you see it because it will disappear as soon as you leave the page
1. Enable SSO for both GitHub and Microsoft organizations for the token
1. In the [BCDE secrets page](https://github.com/microsoft/componentdetection-bcde/settings/secrets) click update on **GH_PRIVATE_REPO_PAT**
1. Paste in your new token
1. Click **Update Secret**
