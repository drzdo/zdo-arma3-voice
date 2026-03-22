#!/bin/bash
# Create and push the next patch version tag
set -e

# Get the latest version tag
latest=$(git tag -l 'v*' --sort=-v:refname | head -1)

if [ -z "$latest" ]; then
    next="v0.1.0"
else
    # Parse major.minor.patch
    version="${latest#v}"
    IFS='.' read -r major minor patch <<< "$version"
    patch=$((patch + 1))
    next="v${major}.${minor}.${patch}"
fi

echo "Latest: ${latest:-none} → Next: $next"
read -p "Tag and push $next? [y/N] " confirm
if [[ "$confirm" != "y" && "$confirm" != "Y" ]]; then
    echo "Aborted."
    exit 0
fi

# Update version in .hemtt/project.toml
sed -i '' "s/^major = .*/major = $major/" .hemtt/project.toml
sed -i '' "s/^minor = .*/minor = $minor/" .hemtt/project.toml
sed -i '' "s/^patch = .*/patch = $patch/" .hemtt/project.toml
git add .hemtt/project.toml
git commit -m "Bump version to $next"

git tag "$next" && git push origin "$next" && git push origin HEAD
echo "Released $next"
