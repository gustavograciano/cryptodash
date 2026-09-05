# 11 — TLS renovável e cabeçalhos de segurança

**Bloqueador de disponibilidade.** O site sai do ar sozinho em 90 dias.

## Problemas

`deploy/nginx/nginx.conf`, bloco da porta 80:

```nginx
server {
    listen 80;
    server_name _;
    return 301 https://$host$request_uri;
}
```

O `return 301` é incondicional — inclusive para `/.well-known/acme-challenge/`. O certbot usa
esse caminho no desafio HTTP-01, recebe um redirect e **a renovação falha**. Certificado
Let's Encrypt dura 90 dias.

Além disso: **nenhum** cabeçalho de segurança (HSTS, X-Content-Type-Options, X-Frame-Options,
Referrer-Policy, CSP), nenhum rate limit na borda, e `server_name cryptodash.local` fixo no
arquivo.

## Critério de pronto

- [ ] `/.well-known/acme-challenge/` servido em HTTP, sem redirect
- [ ] HSTS, `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy` e CSP presentes
- [ ] Rate limit na borda para `/api/`
- [ ] Configuração validada com `nginx -t` em container

## Cuidado

CSP restritiva demais quebra o frontend (estilos inline do React, Recharts). Começar permissiva
o suficiente para funcionar e apertar depois — CSP que quebra a aplicação é removida na
primeira emergência e nunca volta.

---

## Implementado

- `/.well-known/acme-challenge/` servido em HTTP por `location ^~`, **antes** do redirect, que
  passou a viver em `location /`. É o que destrava a renovação do certificado.
- HSTS, `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`, `Permissions-Policy` e
  CSP, todos com `always` para valerem também em resposta de erro.
- `limit_req_zone` de 20 r/s por IP, aplicado em `/api/` com burst de 40.
- O domínio saiu do arquivo: `nginx.conf` virou `nginx.conf.template` e a imagem oficial faz
  `envsubst` sobre `${PUBLIC_HOST}`. O compose passa `NGINX_ENVSUBST_OUTPUT_DIR=/etc/nginx` e
  exige `PUBLIC_HOST` com `:?`.
- Volume novo `./deploy/certbot-www:/var/www/certbot` para o desafio.

### Sobre a CSP

`style-src` permite `'unsafe-inline'` de propósito: React e Recharts injetam estilo inline, e
uma CSP que quebra a aplicação é removida na primeira emergência e nunca volta. O que importa
aqui é `script-src 'self'` — é isso que contém exfiltração de token por XSS, que é o vetor real
dado que o JWT vive em `localStorage`.

## ⚠️ Verificação PENDENTE

O critério da spec exige `nginx -t` em container e **isso não foi executado**: o daemon do
Docker parou de responder durante esta sessão. A configuração foi revisada à mão, mas
**não está validada**.

Antes de subir:

```bash
docker run --rm -e PUBLIC_HOST=seu.dominio -e NGINX_ENVSUBST_OUTPUT_DIR=/etc/nginx \
  -v "$PWD/deploy/nginx/nginx.conf.template:/etc/nginx/templates/nginx.conf.template:ro" \
  nginx:1.27-alpine /docker-entrypoint.sh nginx -t
```

Um ponto a conferir nessa validação: o `envsubst` da imagem substitui apenas variáveis que
existam no ambiente, então `$host`, `$scheme` e afins passam intactos — mas isso deixa de valer
se alguém definir uma variável de ambiente com esses nomes.
