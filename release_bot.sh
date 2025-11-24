#!/bin/bash
# release-to-gitea.sh

WORK_REMOTE="https://github.com/wwevo/chrani-bot-tng-mod.git"
GITEA_REMOTE="https://code.notjustfor.me/wwevo/chrani-bot-tng-mod.git"
RELEASE_TAG=$(grep -oP '(?<=<Version value=")[^"]+' ./CHRANIBotTNG/ModInfo.xml)

if [ -z "$RELEASE_TAG" ]; then
    echo ".xml not found or wrong ModInfo version."
    exit 1
fi

# Create temp directory
TEMP_DIR=$(mktemp -d)
cd "$TEMP_DIR"

# Clone from work remote
git clone "$WORK_REMOTE" release_repo
cd release_repo

# Squash all commits into one
git reset $(git commit-tree HEAD^{tree} -m "Release $RELEASE_TAG")

# Change author
git commit --amend --author="wwevo <code@notjustfor.me>" --no-edit

# Tag the release
git tag "$RELEASE_TAG"

# Push to Gitea
git remote add gitea "$GITEA_REMOTE"
git push gitea master --force
git push gitea "$RELEASE_TAG"

# Cleanup
cd ~
rm -rf "$TEMP_DIR"

echo "Release $RELEASE_TAG pushed to Gitea"
