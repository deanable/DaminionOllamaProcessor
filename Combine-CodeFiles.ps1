# Set the working directory to the location the script is run from (your solution's root folder).
$workingDir = $PSScriptRoot

# Set the output file name with a timestamp.
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$outputFile = Join-Path -Path $workingDir -ChildPath "Combined_Codebase_$timestamp.txt"

# Define the file extensions to include in the bundle.
$extensions = @("*.cs", "*.xaml", "*.csproj", "*.sln", "*.vdproj") # Common C# project files

# Define directories to exclude from the search. This is the most important change.
$excludeDirs = @("bin", "obj", ".vs")

# Clear the output file if it already exists to start fresh.
Clear-Content -Path $outputFile -ErrorAction SilentlyContinue

Write-Output "Searching for files in: $workingDir"
Write-Output "Excluding directories: $($excludeDirs -join ', ')"
Write-Output "..."

# Gather all matching files recursively, but exclude the specified directories.
$codeFiles = Get-ChildItem -Path $workingDir -Include $extensions -Recurse -File -Exclude $excludeDirs

Write-Output "Found $($codeFiles.Count) files to combine."

# Combine the file contents into the single output file using the new, structured format.
foreach ($file in $codeFiles) {
    # 1. Get the full path of the file.
    $filePath = $file.FullName

    # 2. Read the entire content of the file as a single raw string to preserve formatting.
    $fileContent = Get-Content -Path $filePath -Raw -Encoding UTF8

    # 3. Create the structured block for this file.
    # The <![CDATA[...]]> block ensures the content is preserved perfectly.
    $fileBlock = @"

<File Path="$filePath">
<![CDATA[
$fileContent
]]>
</File>

"@

    # 4. Append the block to our output file.
    Add-Content -Path $outputFile -Value $fileBlock -Encoding UTF8
}

Write-Output "--------------------------------------------------"
Write-Output "SUCCESS!"
Write-Output "Combined codebase file created at: $outputFile"
Write-Output "--------------------------------------------------"