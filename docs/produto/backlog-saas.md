# CryptoDash — backlog proposto para o piloto SaaS

**Data:** 23/09/2026. **Status:** proposto; nenhum item abaixo foi implementado nesta análise.
**Referência:** [viabilidade, pesquisa e estimativas](viabilidade-saas.md).

Prioridade: P0 bloqueia dados de clientes ou a confiabilidade do núcleo; P1 bloqueia a proposta comercial; P2 depende de validação posterior. Tamanho P/M/G é relativo, não prazo fechado. As horas consolidadas estão no plano principal; não somar estas classificações como estimativas adicionais.

## S00 — comprovar a dor e o primeiro canal

**Fase:** F0. **Prioridade:** P0 comercial. **Tamanho:** M. **Responsável sugerido:** fundador.

Como responsável pelo produto, quero observar como investidores conferem suas carteiras, para escolher uma tarefa pela qual aceitem pagar.

- [ ] Registrar 15 entrevistas com investidores e cinco com contadores, separando observações, frases, interpretações e hipóteses.
- [ ] Obter cinco amostras de dados com autorização/anonimização e política de descarte.
- [ ] Comparar a mesma tarefa em duas alternativas atuais, incluindo esforço de importação e correção.
- [ ] Definir fonte inicial, operações suportadas, resultado entregue e oferta mensal.
- [ ] Obter cinco compromissos explícitos de piloto pago e registrar por que os demais recusaram.
- [ ] Decidir continuar, reduzir escopo, pivotar ou encerrar antes de financiar F1–F5.

**Dependências:** acesso a participantes. **Entregáveis:** matriz de dores, corpus de arquivos, proposta e decisão registrada. Não automatizar contato comercial sem autorização.

## S01 — conta, carteira e isolamento

**Fase:** F1. **Prioridade:** P0. **Tamanho:** G. **Responsável sugerido:** fundador/desenvolvimento.

Como cliente, quero que somente pessoas autorizadas acessem meus registros, para usar o serviço com privacidade.

- [ ] Vincular carteiras, contas de origem, operações, posições, metas, planos e importações ao proprietário.
- [ ] Resolver conta a partir da identidade validada; ignorar ou rejeitar tentativa do cliente de escolher conta alheia.
- [ ] Dado um cliente A, quando solicitar um ID do cliente B em qualquer leitura, alteração, exclusão, recálculo ou exportação, nenhum dado de B é retornado ou modificado.
- [ ] Garantir unicidade de posições/metas e idempotência dentro do escopo correto; verificar FKs entre objetos de contas diferentes.
- [ ] Jobs, cache e arquivos exportados preservam o mesmo isolamento.
- [ ] Ao trocar de conta ou sair, limpar cache autenticado do navegador; não exibir dados da sessão anterior.
- [ ] Migrar o histórico existente para a conta original, conferindo quantidade, custo e totais antes/depois; ensaiar recuperação da migração.
- [ ] Exercitar testes com PostgreSQL real, duas contas e operações concorrentes.

**Dependências:** modelo do núcleo. **Pontos de partida:** Domain/Entities, AppDbContext, PortfolioService, AportsService, ReconciliationService e jobs.

## S02 — identidade e ciclo de sessão

**Fase:** F1. **Prioridade:** P0. **Tamanho:** M.

Como cliente, quero criar/acessar minha conta e recuperá-la com segurança, para não depender de uma senha provisionada manualmente.

- [ ] Escolher provedor gerenciado ou identidade mantida pela aplicação, documentando custo e manutenção.
- [ ] Para piloto por convite, convite expira e não permite escolher privilégios; cadastro público só após proteção contra abuso.
- [ ] E-mail verificado e recuperação com token de uso único e validade curta.
- [ ] Sessões podem expirar e ser revogadas; recuperação de senha invalida sessões conforme política definida.
- [ ] Substituir persistência de token de longa duração por desenho explícito de sessão; se usar cookie, configurar HttpOnly/Secure/SameSite e defesa contra CSRF conforme o fluxo.
- [ ] Validar limitação de login atrás do proxy confiável, sem confiar arbitrariamente em cabeçalhos enviados pela internet.
- [ ] Exigir proteção adicional para acesso administrativo; registrar ações de suporte sem expor segredos ou patrimônio em logs.

**Dependências:** S01. **Pontos de partida:** AuthController, AuthService, JwtTokenService, Program, authStore e apiClient.

## S03 — separar plano, simulação e operação comprovada

**Fase:** F2. **Prioridade:** P0. **Tamanho:** M.

Como investidor, quero registrar exatamente o que negociei, para que o saldo e o custo correspondam ao extrato.

- [ ] Uma sugestão de aporte não altera a carteira.
- [ ] Confirmação manual exige quantidade, preço/total, taxa, moeda da taxa, origem e data reais, com consistência entre os campos.
- [ ] Repetir a mesma confirmação não duplica operação; chave reutilizada com conteúdo diferente produz conflito compreensível.
- [ ] A estimativa de cotação não substitui os valores negociados; registros legados inferidos têm procedência/qualidade identificada.
- [ ] Cotação ausente não aparece como patrimônio zero; cotação antiga informa origem/horário e restringe simulação conforme política.
- [ ] Dado custo e taxa de referência, a posição resultante fecha com o esperado após persistência no PostgreSQL.
- [ ] Novo cliente define suas próprias metas; nenhuma alocação pessoal seedada é apresentada como orientação padrão.
- [ ] Moeda retirada das metas deixa de influenciar o cálculo; conjunto de metas não conserva resíduos silenciosos da configuração anterior.

**Dependências:** S01. **Pontos de partida:** AportsService, AportExecuteModal, DcaAllocator, DbSeeder e UpdateAllocationAsync.

## S04 — histórico financeiro e métricas com significado correto

**Fase:** F2; métricas avançadas em P2. **Prioridade:** P0. **Tamanho:** M/G.

Como cliente, quero entender de onde vieram minhas posições e resultados, para conferir e corrigir erros.

- [ ] Definir tabela de operações suportadas: compra, venda, transferência própria e taxas; swaps e rendimentos ficam pendentes enquanto não houver regra completa.
- [ ] Transferir entre duas contas próprias não cria compra fictícia nem ganho; preservar vínculo e custo pertinente.
- [ ] Taxa em ativo diferente tem quantidade/moeda registradas; caso não suportado fica pendente, sem conversão silenciosa.
- [ ] Posição inicial sem custo comprovado fica marcada como incompleta; não inventar base histórica pelo preço de hoje.
- [ ] Alteração/exclusão mantém trilha de quem alterou, quando e origem; reprocessamento é determinístico.
- [ ] Renomear o P&L atual para resultado não realizado das posições; explicar limitações da variação de mercado de 24h.
- [ ] Reconciliação compara razão/posição e permite conferir referência externa, identificando lacunas de histórico.
- [ ] Paginação/ordenação ocorrem no banco após filtros de propriedade; volume de importação não carrega todo o histórico por requisição de lista.

**P2 condicionado:** resultado realizado, snapshots de patrimônio e retornos ajustados a aportes/saques, com fórmulas, exemplos e cobertura de taxas. Não chamar valorização de patrimônio de rentabilidade sem separar fluxos.

**Dependências:** S01 e S03. **Pontos de partida:** TransactionType, Transaction, HoldingRecalculator, PortfolioService e ReconciliationService.

## S05 — importação e primeira carteira conferida

**Fase:** F3. **Prioridade:** P1. **Tamanho:** G.

Como cliente, quero importar meu extrato e revisar problemas, para começar sem redigitar todo o histórico.

- [ ] Implementar um formato real escolhido em S00, além do modelo CSV genérico; segundo adaptador só quando houver amostras e demanda.
- [ ] Identificar conta de origem, versão de formato, fuso, separador decimal, ativo e IDs externos.
- [ ] Antes de confirmar, mostrar totais, linhas válidas, rejeitadas, duplicadas e pendentes.
- [ ] Upload tem limites de tamanho/linhas e validação de conteúdo; CSV não executa fórmulas no servidor.
- [ ] Reimportar o mesmo arquivo não muda os saldos nem duplica operações; comportamento entre formatos sobrepostos é especificado.
- [ ] Dados não suportados ficam pendentes com explicação; nenhum erro vira saldo “correto” por descarte silencioso.
- [ ] Confirmação é recuperável e não deixa lote parcialmente visível como concluído; desfazer lote preserva auditoria e recalcula posições.
- [ ] Conferir casos de referência contra extratos, incluindo transferência, taxa, lançamento retroativo e arredondamento no banco.
- [ ] Em teste observado, medir tempo até primeira carteira conferida, assistência e erros; objetivo experimental de mediana até 15 minutos para o formato suportado.

**Dependências:** S00–S04. **Entregáveis:** matriz de compatibilidade, arquivos de teste anonimizados, fluxo de revisão e manual curto.

## S06 — exportação e controle dos próprios dados

**Fase:** F3/F4. **Prioridade:** P0 privacidade / P1 valor comercial. **Tamanho:** M.

Como cliente, quero obter meus registros com origem e pendências, para conferir fora do sistema e compartilhar com um profissional.

- [ ] Exportar operações, custos disponíveis, taxas, fonte, moeda e data; separar dados estimados/incompletos.
- [ ] Exportação pertence à conta solicitante e neutraliza conteúdo que possa virar fórmula ao abrir em planilhas.
- [ ] Relatório diz que organiza registros e não é declaração fiscal homologada ou cálculo de imposto.
- [ ] Cancelar assinatura mantém acesso à exportação conforme política comunicada.
- [ ] Solicitação de exclusão executa a política definida para dados ativos, arquivos e expiração de backups, considerando retenções legalmente necessárias.
- [ ] Nenhum compartilhamento com contador é público por padrão; convite profissional/revogação só entram quando esse fluxo for construído.

**Dependências:** S01, S04 e revisão de privacidade. **Escopo inicial:** CSV; PDF e painel do contador após validação.

## S07 — assinatura, cobrança e cancelamento

**Fase:** F4. **Prioridade:** P1. **Tamanho:** M.

Como assinante, quero saber quanto pago e cancelar facilmente, para manter controle sobre a contratação.

- [ ] Um plano inicial com preço, periodicidade, cobertura, limites, teste e política de cancelamento claros.
- [ ] Checkout hospedado; aplicação não armazena dados de cartão.
- [ ] Assinatura no servidor é alterada por evento validado do provedor, nunca pelo retorno de “sucesso” no navegador.
- [ ] Eventos repetidos ou fora de ordem não duplicam cobrança nem ativam período incorreto.
- [ ] Testar contratação, renovação, pagamento recusado, recuperação, cancelamento e reembolso conforme política aplicável.
- [ ] Limites são aplicados no servidor, preservando acesso ao histórico e exportação conforme contrato.
- [ ] Preparar emissão de documento fiscal do próprio SaaS com contador/provedor; não confundir com tributação dos investimentos do cliente.
- [ ] Validar uma contratação real controlada antes da abertura comercial; contabilizar receita recorrente sem inflar por anual antecipado.

**Dependências:** S01/S02 e decisão de oferta. **Piloto assistido:** checkout e ativação administrada são aceitáveis se auditáveis; automatizar todo o autosserviço pode esperar.

## S08 — rotina mensal e retenção

**Fase:** F5. **Prioridade:** P1. **Tamanho:** M.

Como cliente, quero lembrar de revisar meu plano e meus registros, para manter meu controle atualizado.

- [ ] Opt-in, canal inicial de e-mail, preferência de frequência, fuso e cancelamento de notificações.
- [ ] Lembrete não é enviado em duplicidade após reinício/retry; falha de entrega fica observável.
- [ ] Usuário consegue marcar aporte realizado, adiado ou pulado, sem criar operações fictícias.
- [ ] Revisão mensal destaca pendências, alterações e progresso em relação às metas do usuário.
- [ ] Instrumentar ativação, primeira importação conferida, registro/revisão mensal, exportação, pagamento, renovação e cancelamento sem enviar valores patrimoniais desnecessários a analytics.
- [ ] Acompanhar duas coortes/ciclos quando disponíveis e registrar minutos de suporte por cliente.

**Dependências:** S01–S07. **Pontos de partida:** AportReminderJob, AportsService e NextAportCard. WhatsApp/push são posteriores à evidência de demanda.

## S09 — prontidão operacional e licenças

**Fase:** inicia com F1, concluída antes do piloto com dados reais. **Prioridade:** P0. **Tamanho:** G.

Como responsável pelo serviço, quero detectar falhas e recuperar dados, para sustentar a confiança de quem paga.

- [ ] Atualizar para runtime suportado antes do fim do suporte .NET 8; validar bibliotecas, containers e migrations.
- [ ] CI executa suíte atual, build e testes essenciais de PostgreSQL; separar dados e segredos de staging/produção.
- [ ] Backup criptografado fora do host; ensaiar restauração em ambiente limpo e documentar resultado.
- [ ] Definir e verificar metas internas iniciais de RPO até 24h e RTO até 4h para o piloto; revisar com necessidades reais antes de prometer SLA.
- [ ] Alertar falhas de importação/jobs/cobrança e cotações antigas por ativo relevante, não apenas pelo preço mais recente entre todas as moedas.
- [ ] Garantir executor único ou coordenação persistente de jobs; testar reinício sem duplicar operações/notificações.
- [ ] Configurar limites de upload, requisições, chamadas de dados e geração IA; medir custo e uso por conta sem logar segredos.
- [ ] Confirmar licença dos fornecedores, atribuição, permissões de cache/redistribuição e retenção; habilitar somente integrações aprovadas para o uso comercial.
- [ ] Revisar termos, privacidade, fronteira de atividade financeira e comunicação comercial com profissionais adequados.
- [ ] Documentar suporte, acesso administrativo, resposta a incidente e procedimento de cancelamento/portabilidade.

**Dependências:** todas as funcionalidades habilitadas. **Observação:** esta entrega começa cedo; não é um checklist deixado para o fim.

## S10 — expansões condicionadas

**Prioridade:** P2. **Tamanho:** reestimar por expansão.

| Expansão | Evidência mínima antes de iniciar | Restrição |
|---|---|---|
| Uma API de exchange somente leitura | Usuários pagos perdem tempo com CSV e a fonte é frequente | Criptografia de credenciais, permissões mínimas, desconexão, auditoria e reconciliação; sem ordens/saques |
| Painel para contador | Ao menos três profissionais aceitam piloto pago com fluxo definido | Consentimento, múltiplos clientes, acesso revogável e trilha de auditoria |
| Relatório fiscal automatizado | Problema fiscal específico pago e especialista responsável disponível | Regras e jurisdições versionadas, casos validados e acompanhamento normativo |
| IA contextual | Uso recorrente do resumo existente e benefício medido | Fontes, limites de custo, privacidade e nenhuma promessa de acerto de preço |
| Mais ativos/fontes | Pedidos recorrentes de clientes retidos | Identidade do ativo, manutenção do adaptador e licença viável |

## Critério conjunto de abertura do piloto

1. S01/S02/S03 e a parcela de S04 necessária ao formato escolhido concluídos.
2. Primeiro importador conferido, exportação disponível e casos incompletos claramente marcados.
3. Zero acesso cruzado nos testes e zero divergência monetária inexplicada nos casos de referência.
4. Backup externo restaurado, termos/privacidade e licenças das funções habilitadas resolvidos.
5. Oferta, suporte e cancelamento claros; pagamento controlado validado.
6. Dez participantes recrutados, medição preparada e revisão de decisão após dois ciclos mensais.

Depois desse marco, priorizar o que impede ativação e renovação. Volume de funcionalidades não substitui evidência de utilidade e pagamento.
