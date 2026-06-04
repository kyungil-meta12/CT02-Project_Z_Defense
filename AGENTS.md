# Agent Instructions

- When reading files that may contain Korean text from PowerShell, force UTF-8 output and file decoding so comments and strings are not misread:
  ```powershell
  [Console]::OutputEncoding = [System.Text.Encoding]::UTF8
  Get-Content -LiteralPath "path\to\file" -Encoding UTF8
  ```
