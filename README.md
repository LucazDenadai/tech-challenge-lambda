# tech-challenge-lambda

Function Serverless (.NET) de autenticação via CPF — recebe um CPF, valida, consulta o cliente no RDS e emite um JWT compatível com o middleware de auth do [Tech-challenge](https://github.com/LucazDenadai/tech-challenge) (Atendimento).

Documentação arquitetural completa (ADRs, RFCs, diagramas) em [tech-challenge-docs](https://github.com/LucazDenadai/tech-challenge-docs).

Diagramas relevantes para este repositório:

| Diagrama | Conteúdo |
|---|---|
| [Sequência — Autenticação via CPF](https://github.com/LucazDenadai/tech-challenge-docs/blob/main/diagramas/diagrama-sequencia-autenticacao.md) | Fluxo completo: CPF → esta função → JWT → consumo de rota protegida |
| [Componentes](https://github.com/LucazDenadai/tech-challenge-docs/blob/main/diagramas/diagrama-componentes.md) | Onde esta função se encaixa na arquitetura de nuvem |

Ver também [ADR-013](https://github.com/LucazDenadai/tech-challenge-docs/blob/main/adr/ADR-013-autenticacao-authorize-aspnet-nao-api-gateway.md): a validação do JWT emitido aqui acontece no Atendimento (`[Authorize]`), não no API Gateway.

## Decisão: .NET 8, não .NET 10

O resto do projeto usa .NET 10, mas a AWS Lambda ainda não tem runtime gerenciado `dotnet10` — a lista de runtimes suportados pelo provider Terraform da AWS vai até `dotnet8`. Este repositório usa `net8.0` (só aqui) por essa restrição da plataforma, não por escolha de padrão.

## O que é

| Componente | Descrição |
|---|---|
| `Function.cs` | Handler — recebe `APIGatewayProxyRequest`, orquestra validação/consulta/JWT |
| `CpfValidator.cs` | Validação de CPF (dígito verificador) — reimplementação mínima, sem dependência do assembly `Domain` de `Tech-challenge` (evita acoplamento cross-repo por uma função pura) |
| `ClienteRepository.cs` | Consulta direta ao RDS via Npgsql (sem EF Core, por peso/cold start) |
| `JwtGenerator.cs` | Gera JWT HMAC-SHA256 compatível com `JwtTokenService` do Atendimento — mesma chave, issuer e audience |

## Fluxo

1. Recebe `{ "cpf": "..." }` no body da requisição (via API Gateway)
2. CPF inválido (formato) → **400**, sem consultar o banco
3. CPF válido, cliente inexistente → **404**
4. CPF válido, cliente existente mas inativo → **403**
5. CPF válido, cliente ativo → **200** com `{ "token": "<jwt>" }`

O JWT emitido usa a claim `role=Cliente` — distinta dos tokens de funcionário (`Admin`/`Atendente`/`Mecanico`) emitidos pelo Atendimento. A integração com a rota protegida real (acompanhamento de OS) é feita em [CARD-30](https://github.com/LucazDenadai/tech-challenge-docs/blob/main/cards/05-fase3-aws/CARD-30-api-gateway-integracao.md), validada no Atendimento via `[Authorize(Roles="Cliente")]` ([ADR-013](https://github.com/LucazDenadai/tech-challenge-docs/blob/main/adr/ADR-013-autenticacao-authorize-aspnet-nao-api-gateway.md)).

### Payload de entrada e saída

**Requisição:**

```
POST /atendimento/auth/cpf
Content-Type: application/json

{ "cpf": "12345678901" }
```

**200 OK** — cliente encontrado e ativo:

```json
{ "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiI..." }
```

Claims do token: `sub` (ID do cliente), `role` (`Cliente`), `iss`, `aud`, `exp` (60 min por padrão).

**400 Bad Request** — CPF com formato inválido (não passa na validação de dígito verificador):

```json
{ "erro": "CPF inválido." }
```

**404 Not Found** — CPF válido, mas nenhum cliente cadastrado com esse documento:

```json
{ "erro": "Cliente não encontrado." }
```

**403 Forbidden** — CPF válido, cliente cadastrado, mas inativo:

```json
{ "erro": "Cliente inativo." }
```

## Variáveis de ambiente (runtime da Lambda)

| Variável | Descrição |
|---|---|
| `DB_CONNECTION_STRING` | Connection string do RDS (schema `atendimento`, tabela `Clientes`) |
| `JWT_KEY` | Chave HMAC-SHA256 — deve ser a mesma usada em `Tech-challenge` (`JWT_KEY`) |
| `JWT_ISSUER` | Issuer do JWT — deve bater com `Jwt:Issuer` do Atendimento (default: `oficina-atendimento`) |
| `JWT_AUDIENCE` | Audience do JWT — deve bater com `Jwt:Audience` do Atendimento (default: `oficina-atendimento-api`) |
| `JWT_EXPIRACAO_MINUTOS` | Expiração do token em minutos (default: `60`) |

Todas injetadas como variável de ambiente da Lambda via Terraform (`infra/main.tf`), a partir de GitHub Secrets do pipeline — nunca hardcoded.

## Cold start

Não medido em produção ainda (a função não foi implantada — deploy real acontece no CARD-27/pipeline). Estimativa baseada nas otimizações aplicadas e no tamanho do pacote publicado:

- Pacote publicado (`dotnet publish -r linux-x64`): **~5.4 MB**
- `PublishReadyToRun=true` habilitado no `.csproj` — pré-compila IL para reduzir JIT no cold start
- Runtime gerenciado `dotnet8` (não container customizado) — evita overhead de inicialização de container
- Dependências mínimas: Npgsql (sem EF Core), `System.IdentityModel.Tokens.Jwt`, sem framework web

Ordem de grandeza esperada para Lambda .NET gerenciada com essas características: **300–800ms** de cold start, **<50ms** em invocações subsequentes (warm). Este valor precisa ser confirmado com medição real após o primeiro deploy — atualizar esta seção com o número observado.

## Infraestrutura (Terraform)

Diferente do que o card original sugeria (SAM/`serverless.yml`), a infraestrutura é provisionada via **Terraform**, consistente com `tech-challenge-infra-k8s` e `tech-challenge-infra-db` — e é o que o enunciado oficial do Tech Challenge exige ("Terraform para provisionamento").

| Recurso | Descrição |
|---|---|
| `aws_lambda_function` | Runtime `dotnet8`, roda dentro da VPC (subnets privadas do `infra-k8s`) |
| IAM role | Permissões mínimas: assumir role + `AWSLambdaVPCAccessExecutionRole` (ENI na VPC, logs) |
| Security group | Sem ingress (Lambda não recebe conexão direta); egress liberado para alcançar o RDS |
| Regra no SG do RDS | Libera porta 5432 a partir do SG da Lambda — gerenciada aqui, não em `infra-db`, para não criar dependência circular entre os dois repositórios |

Depende do state remoto de `tech-challenge-infra-k8s` (VPC/subnets) e `tech-challenge-infra-db` (security group do RDS) — rode o apply desses dois primeiro.

### Como usar

```bash
# 1. Publicar o binário da Lambda
dotnet publish src/OficinaMecanica.Auth.Lambda -c Release -r linux-x64 --self-contained false
cd src/OficinaMecanica.Auth.Lambda/bin/Release/net8.0/linux-x64/publish
zip -r ../../../../../../artifacts/lambda.zip .
cd -

# 2. Provisionar
cd infra
cp terraform.tfvars.example terraform.tfvars
# edite terraform.tfvars com os valores reais

terraform init
terraform plan
terraform apply
```

**Lembrete de custo (ADR-010/ADR-011):** cobrança por invocação, irrelevante no volume de testes — mas ainda assim rode `terraform destroy` ao final de cada sessão junto com os outros repositórios de infra.

## Testes

```bash
dotnet test
```

13 testes cobrindo os 4 cenários do fluxo (CPF inválido, cliente não encontrado, cliente inativo, sucesso) e a validação de CPF isoladamente (dígito verificador, dígitos repetidos, tamanho inválido).

## O que NÃO commitar

| Arquivo/Pasta | Motivo |
|---|---|
| `infra/terraform.tfvars` | Contém connection string e chave JWT |
| `infra/terraform.tfstate` / `.backup` | Não se aplica — state fica remoto no S3 |
| `artifacts/` | Pacote publicado da Lambda, gerado localmente antes do apply |
| `bin/`, `obj/` | Build local |

Todos já estão no `.gitignore`.
