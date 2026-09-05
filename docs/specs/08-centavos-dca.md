# 08 — Centavos do primeiro aporte

**Invariante 2.** Cinco testes vermelhos hoje.

## Problema

`DcaAllocator.Allocate`, no ramo `totalCurrent == 0` (primeiro aporte de um usuário novo),
retorna **antes** da reconciliação de arredondamento e antes do piso por moeda:

```csharp
if (totalCurrent == 0)
{
    return rows.Select(r => new DcaOutput(
        r.CoinId, r.Symbol,
        Math.Round(aporteBrl * (r.TargetPct / 100m), 2),   // <- soma pode não fechar
        r.CurrentValueBrl, r.TargetValue, r.Gap)).ToList();
}
```

Casos reais capturados pelos testes: aporte de R$ 1.825,36 distribui R$ 1.825,37; aporte de
R$ 0,05 com três alvos de 33,33/33,33/33,34 distribui R$ 0,06.

## Critério de pronto

- [ ] `Invariante2c` e `Invariante2d` verdes em todas as sementes
- [ ] `Invariante2e` (nenhuma alocação negativa) continua verde
- [ ] `Invariante2f` (moeda com alvo positivo não some) continua verde
- [ ] A soma fecha **exatamente** com o aporte, inclusive para valores pequenos

## Abordagem

Fazer o ramo do primeiro aporte passar pelo mesmo caminho de reconciliação do ramo normal:
extrair o ajuste de centavos e o piso por moeda para um trecho comum, aplicado aos dois ramos
antes do retorno.

Cuidado com o piso de R$ 5: em aporte pequeno (R$ 0,05) o piso não pode zerar todas as linhas.
A regra atual só move valor quando existe destino maior; validar que o comportamento
permanece sensato e que a soma continua fechando.
