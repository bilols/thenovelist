````markdown
# 📄 ListGitHubRawUrls.ps1

> **A PowerShell script to list the raw.githubusercontent.com URL for every file in a GitHub repository’s default branch.**  
> Share this file in your repo to let others quickly generate a list of raw file URLs.

---

## 📋 Requirements

- **PowerShell** 5.1 or newer (Windows)
- **Public** GitHub repository (no authentication).  
  > *For private repositories*, see [Authentication](#🔐-authentication) below.  
- Script file saved with **UTF-8** encoding and **ASCII** characters only.

---

## ⚙️ Usage

```powershell
.\ListGitHubRawUrls.ps1 -Owner <OWNER> -Repo <REPO>
````

Redirect output to a file if desired:

```powershell
.\ListGitHubRawUrls.ps1 -Owner octocat -Repo Hello-World > urls.txt
```

---

## 🎯 Parameters

| Parameter | Description                         | Required |
| --------- | ----------------------------------- | :------: |
| `-Owner`  | GitHub account or organization name |    Yes   |
| `-Repo`   | GitHub repository name              |    Yes   |

---

## 📝 The Script

```powershell
<#
.SYNOPSIS
    Lists the raw.githubusercontent.com URL for every file in a GitHub repo’s default branch.

.DESCRIPTION
    Uses the Git Trees API to fetch the repository’s file tree recursively,
    then constructs and outputs the raw file URLs, one per line.

.PARAMETER Owner
    GitHub account or organization owning the repository (e.g., "octocat").

.PARAMETER Repo
    Name of the repository (e.g., "Hello-World").

.EXAMPLE
    PS> .\ListGitHubRawUrls.ps1 -Owner octocat -Repo Hello-World
#>

param (
    [Parameter(Mandatory = $true)]
    [string]$Owner,

    [Parameter(Mandatory = $true)]
    [string]$Repo
)

# Build the Git Trees API URL, using 'HEAD' to reference the default branch
$ApiUrl = "https://api.github.com/repos/$Owner/$Repo/git/trees/HEAD?recursive=1"

# GitHub requires a User-Agent header for REST API requests
$Headers = @{ 'User-Agent' = 'PowerShellScript' }

try {
    # Fetch the tree structure (up to 100,000 items, 7 MB max)
    $Response = Invoke-RestMethod -Uri $ApiUrl -Headers $Headers

    # Iterate through each tree object; 'blob' indicates a file
    foreach ($Item in $Response.tree) {
        if ($Item.type -eq 'blob') {
            # Construct the raw URL: raw.githubusercontent.com/{owner}/{repo}/HEAD/{path}
            $RawUrl = "https://raw.githubusercontent.com/$Owner/$Repo/HEAD/$($Item.path)"
            # Output each URL on its own line, no bullets or extra formatting
            Write-Output $RawUrl
        }
    }
}
catch {
    Write-Error "Failed to fetch or process repository tree: $_"
    exit 1
}
```

---

## 🔧 Customization

* **Target a specific branch**
  Replace `HEAD` in both the `$ApiUrl` and `$RawUrl` definitions with your branch name:

  ```powershell
  $ApiUrl   = "https://api.github.com/repos/$Owner/$Repo/git/trees/my-branch?recursive=1"
  $RawUrl   = "https://raw.githubusercontent.com/$Owner/$Repo/my-branch/$($Item.path)"
  ```

* **Authentication for private repos**
  Add a GitHub Personal Access Token:

  ```powershell
  $Headers['Authorization'] = "token YOUR_PERSONAL_ACCESS_TOKEN"
  ```

---

> *Feel free to share issues or improvements!* 👋

```
::contentReference[oaicite:0]{index=0}
```
