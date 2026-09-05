# 10 — Segredos fora do versionamento e do contexto de build

**Achados da auditoria de segurança, ainda sem correção.** Esforço trivial, dano alto.

## Problemas

1. **`.gitignore` não cobre `backups/` nem `deploy/letsencrypt/`.** Um `git add .` no servidor
   commita dumps do banco inteiro e a **chave privada TLS** num repositório descrito como
   portfólio.
2. **`.dockerignore` não exclui `.env`, `.env.production` nem `*.db`.** Entram no contexto de
   build e podem acabar em camada de imagem.
3. **`docs/capture.mjs:10`** — `const PASSWORD = process.env.SEED_PASSWORD ?? 'ChangeMe@123';`
   senha default em arquivo versionado.
4. **`README.md:93` afirma o contrário do que acontece:** "Secrets nunca commitados
   (`.env*` no `.gitignore`)" — enquanto `.env.example` e `.env.production.example`, ambos
   versionados, carregam `SEED_PASSWORD=ChangeMe@123`.

## Critério de pronto

- [ ] `backups/`, `deploy/letsencrypt/` e `*.pem` ignorados pelo git
- [ ] `.env*`, `*.db` e `backups/` fora do contexto de build Docker
- [ ] `capture.mjs` exige a senha por variável de ambiente, sem default
- [ ] README descreve a realidade: os `.example` são versionados e contêm placeholders
- [ ] Verificação: `git check-ignore` confirma cada caminho
