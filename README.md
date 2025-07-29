# bunny.net-storage-upload

Este repositório contém um utilitário para upload e gerenciamento de arquivos em um Storage Zone do Bunny.net, com suporte a remoção de arquivos antigos, integração com GitHub Actions e execução via Docker.

## Visão Geral

O projeto principal está implementado em .NET 8, e seu objetivo é facilitar o envio automatizado de arquivos para o Bunny.net Storage, sendo ideal para pipelines de CI/CD, publicação de sites estáticos ou distribuição de conteúdos em larga escala. Ele pode ser utilizado tanto localmente quanto em automações via GitHub Actions.

## Funcionalidades

- Upload de arquivos/diretórios locais para um Storage Zone do Bunny.net.
- Remoção automática de arquivos remotos que não existem mais localmente, se habilitado.
- Alto grau de paralelismo nas operações (até 50 uploads/deletes simultâneos).
- Retorno de métricas como total de arquivos enviados, deletados, falhas, etc., com integração direta ao GitHub Actions.
- Imagem Docker pronta para uso.

## Como Usar

```yaml
name: Deploy to BunnyCDN

on:
  push:
    branches:
      - main
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
          main_zone: "br"
          storage_zone: ${{ secrets.BUNNY_STORAGE_ZONE }}
          api_key: ${{ secrets.BUNNY_API_KEY }}
          local_path: './deploy-test/dist/'
          remove_old_files: true
```

### GitHub Actions

Este utilitário foi desenhado para fácil integração com pipelines do GitHub Actions, permitindo deploy automatizado de assets, sites estáticos ou outros conteúdos para o Bunny.net Storage.

## Licença

Distribuído sob a licença [GNU GPL v3](LICENSE).

---

Desenvolvido por [rafael-meneses](https://github.com/rafael-meneses).