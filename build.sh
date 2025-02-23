#!/bin/bash

# Get the solution directory
solutionDir=$(pwd)
buildDir="$solutionDir/builds"

# Ensure the builds directory exists
mkdir -p "$buildDir"

# Get all project files in the solution
projects=$(dotnet sln list | tail -n +3) # Skip the first two lines

# Define target runtimes
runtimes=("win-x64" "linux-x64")

# Iterate over projects
for project in $projects; do
    projectName=$(basename "$project" .csproj)

    for runtime in "${runtimes[@]}"; do
        outputPath="$buildDir/$projectName/$runtime"

        echo "Publishing $project for $runtime to $outputPath..."
        dotnet publish "$project" -c Release --self-contained -r "$runtime" -o "$outputPath"
    done
done

echo "Build completed!"
