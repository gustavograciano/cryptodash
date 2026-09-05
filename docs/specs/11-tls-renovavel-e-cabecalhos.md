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
