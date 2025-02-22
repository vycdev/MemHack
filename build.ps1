$solutionDir = Get-Location
$buildDir = "$solutionDir\builds"

# Ensure the builds directory exists
if (!(Test-Path -Path $buildDir)) {
    New-Item -ItemType Directory -Path $buildDir | Out-Null
}

# Get all project files in the solution
$projects = dotnet sln list | Select-Object -Skip 2

# Define target runtimes
$runtimes = @("win-x64", "linux-x64")

foreach ($project in $projects) {
    $projectName = [System.IO.Path]::GetFileNameWithoutExtension($project)

    foreach ($runtime in $runtimes) {
        $outputPath = "$buildDir\$projectName\$runtime"

        Write-Host "Publishing $project for $runtime to $outputPath..."
        dotnet publish $project -c Release --self-contained -r $runtime -o $outputPath
    }
}

Write-Host "Build completed!"
