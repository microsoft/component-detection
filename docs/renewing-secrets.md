# Renewing secrets

Almost all of our [workflows](../.github/workflows) require secrets and those secrets can be invalidated, deleted or expired so we need to know how to renew them.

The secrets in use today in the Component Detection repo can be found [here](https://github.com/microsoft/component-detection/settings/secrets):

* GH_PRIVATE_REPO_PAT

## Renewing GH_PRIVATE_REPO_PAT

1. Click this link: https://github.com/settings/tokens/new
1. (Optional) Name the token COMPONENT_DETECTION_GH_PRIVATE_REPO_PAT. This will make things easier to track in the future
1. Check the following permissions:
    * Full `repo` scope
    * `read:packages` scope
1. Click **Generate token** 
1. Copy and paste that token into notepad once you see it because it will disappear as soon as you leave the page
1. Enable SSO for Microsoft organizations for the token
1. In the [Component Detection secrets page](https://github.com/microsoft/component-detection/settings/secrets) click update on **GH_PRIVATE_REPO_PAT**
1. Paste in your new token
1. Click **Update Secret**
