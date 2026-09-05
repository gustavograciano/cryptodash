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

## Verificação executada

`nginx:1.27-alpine`, template processado pelo entrypoint oficial, upstreams resolvidos com
`--add-host` e certificado autoassinado gerado no container.

### 1. Sintaxe

```
nginx: the configuration file /etc/nginx/nginx.conf syntax is ok
nginx: configuration file /etc/nginx/nginx.conf test is successful
```

### 2. O desafio ACME é servido em HTTP — o defeito que a spec existe para corrigir

| Requisição | Resultado |
|---|---|
| `GET /.well-known/acme-challenge/TESTE123` | **HTTP 200** com o conteúdo do token |
| `GET /portfolio` | **HTTP 301** → `https://…/portfolio` |

O `location ^~` vence o `location /`, então o certbot consegue renovar e todo o resto continua
sendo empurrado para HTTPS.

### 3. Cabeçalhos de segurança

Os seis presentes na resposta HTTPS, e com `always` — valem também em resposta de erro:

```
Strict-Transport-Security: max-age=31536000; includeSubDomains
X-Content-Type-Options: nosniff
X-Frame-Options: DENY
Referrer-Policy: strict-origin-when-cross-origin
Permissions-Policy: geolocation=(), microphone=(), camera=()
Content-Security-Policy: default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; …
```

### 4. O risco do envsubst não se concretizou

Era a dúvida registrada aqui: se o `envsubst` da imagem comeria as variáveis do próprio nginx.
No arquivo gerado elas estão **intactas** — `$binary_remote_addr`, `$uri`, `$host`,
`$request_uri`, `$remote_addr`, `$proxy_add_x_forwarded_for`, `$scheme` — e nenhum
`PUBLIC_HOST` ficou por substituir. O script só substitui nomes que existam no ambiente.

> Nota sobre o teste: `frontend` foi mapeado para `127.0.0.1`, então o nginx faz proxy para a
> própria porta 80 e a resposta observada é o 301 daquele bloco. É artefato do arranjo de
> teste, não da configuração — o que se media ali eram os cabeçalhos, aplicados por cima.

**Status: concluída.**
