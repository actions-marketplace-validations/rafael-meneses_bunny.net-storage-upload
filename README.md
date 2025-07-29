# bunny.net-storage-upload

This repository contains a utility for uploading and managing files in a Bunny.net Edge Storage Zone, with support for removing old files, purging the cache of related Pull Zones, and integration with GitHub Actions.

## Overview
The main project is implemented in .NET 8 and aims to simplify the automated upload of files to Bunny.net Storage. It is ideal for CI/CD pipelines, static website publishing, or large-scale content distribution. It can be used both locally and within GitHub Actions workflows.

## Features
-Upload of local files/directories to a Bunny.net Edge Storage Zone;
-Optional automatic removal of remote files that no longer exist locally;
-High level of parallelism in operations;
-Optional Purge Cache of Pull Zones linked to the Storage;
-Returns metrics such as total files uploaded, deleted, failed, etc., with direct integration into GitHub Actions;
-Ready-to-use Docker image.

## how to Use

```yaml
name: Deploy to BunnyCDN

on:
  workflow_dispatch:

jobs:
  build-and-deploy:
    runs-on: ubuntu-latest
    steps:
      - name: Checkout repository
        uses: actions/checkout@v4
      - name: Setup Node.js
        uses: actions/setup-node@v4
        with:
          node-version: '22'
      - name: Install dependencies and build
        working-directory: ./deploy-test
        run: |
          npm install
          npm run build 

      - name: Sync files to BunnyCDN
        id: bunny-sync 
        uses: ./ 
        with:
          storage_zone: ${{ secrets.BUNNY_STORAGE_ZONE }}
          api_key: ${{ secrets.BUNNY_API_KEY }}
          purge_after_upload: true
          local_path: './deploy-test/dist/'
          remove_old_files: true
```

### GitHub Actions
This utility is designed for seamless integration with GitHub Actions pipelines, enabling automated deployment of assets, static websites, or other content to Bunny.net Storage.

## License
Distributed under the [GNU GPL v3](LICENSE).
---
Developed by [rafael-meneses](https://github.com/rafael-meneses).
