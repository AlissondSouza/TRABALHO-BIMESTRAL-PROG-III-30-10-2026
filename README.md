# Lembretes dos Ancestrais

Aplicação de cadastro de lembretes desenvolvida no trabalho bimestral de **Programação III (UMFG)**. É composta por uma **API REST em .NET 8 (Minimal API)** com banco **MySQL** e por um **front-end em HTML, CSS e JavaScript puro**. O visual da interface e do relatório em PDF foi inspirado no jogo **Dota 2**.

O projeto parte dos repositórios disponibilizados pelo professor:

- [`leicam/umfg-programacao-iii-minimal-api-2026`](https://github.com/leicam/umfg-programacao-iii-minimal-api-2026)
- [`leicam/umfg-programacao-iii-front-end-2026`](https://github.com/leicam/umfg-programacao-iii-front-end-2026)

---

## Sumário

- [Tecnologias](#tecnologias)
- [Estrutura do projeto](#estrutura-do-projeto)
- [Como executar](#como-executar)
- [Endpoints da API](#endpoints-da-api)
- [Comunicação entre front-end e API](#comunicação-entre-front-end-e-api)
- [CORS](#cors)
- [Funcionalidades do front-end](#funcionalidades-do-front-end)
- [Relatório em PDF](#relatório-em-pdf)
- [O que foi alterado](#o-que-foi-alterado)
- [Testes realizados](#testes-realizados)
- [Créditos e licenças](#créditos-e-licenças)

---

## Tecnologias

| Camada | Tecnologia |
|---|---|
| API | .NET 8 · ASP.NET Core Minimal API |
| Acesso a dados | Entity Framework Core 8 · MySql.EntityFrameworkCore |
| Banco de dados | MySQL 8 |
| Relatório | QuestPDF 2026.9.1 |
| Front-end | HTML5 · CSS3 · JavaScript (Fetch API) |
| Fontes | Cinzel · Barlow · Barlow Condensed (Google Fonts, licença OFL) |

---

## Estrutura do projeto

```
TRABALHO-BIMESTRAL-PROG-3/
├── api/
│   ├── umfg.programacaoiii.minimalapi.2026.sln
│   └── umfg.programacaoiii.minimalapi.2026/
│       ├── Contexto/          # MySqlContexto (EF Core)
│       ├── DTO/               # LembreteDTO e LembreteQueryDTO (filtro/paginação)
│       ├── Entidades/         # Lembrete
│       ├── Migrations/        # migrações do banco
│       ├── Fontes/            # fontes .ttf usadas no PDF + licenças OFL
│       ├── Program.cs         # endpoints, CORS, filtro e geração do PDF
│       └── appsettings*.json  # connection string por ambiente
│
├── front-end/
│   ├── index.html             # página única da aplicação
│   ├── css/style.css          # tema Dota 2, responsivo
│   └── js/app.js              # chamadas à API, renderização e validações
│
└── README.md
```

---

## Como executar

### Pré-requisitos

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- MySQL 8 rodando em `localhost:3306`
- Um servidor estático para o front-end (ex.: extensão **Live Server** do VS Code)

### 1. Banco de dados

Informe a senha do seu MySQL na connection string de `api/umfg.programacaoiii.minimalapi.2026/appsettings.Development.json`:

```json
"ConnectionString": "Server=localhost;Port=3306;Database=umfg_lembrete_develop;Uid=root;Pwd=SUA_SENHA;"
```

> Para não gravar a senha em arquivo, também é possível definir a variável de ambiente `ConnectionString`, que tem prioridade sobre o `appsettings`.

Crie o banco aplicando as migrações (dentro de `api/umfg.programacaoiii.minimalapi.2026`):

```bash
dotnet ef database update
```

### 2. Certificado HTTPS de desenvolvimento

O front-end chama a API em `https://localhost:7063`. Confie no certificado de desenvolvimento do .NET:

```bash
dotnet dev-certs https --trust
```

Se o navegador continuar recusando o certificado, limpe e confie novamente:

```bash
dotnet dev-certs https --clean
```

```bash
dotnet dev-certs https --trust
```

### 3. API

Dentro da pasta `api/umfg.programacaoiii.minimalapi.2026` (ou abra o `api/umfg.programacaoiii.minimalapi.2026.sln` no Visual Studio e rode o perfil **https**):

```bash
dotnet run --launch-profile https
```

A API sobe em `https://localhost:7063` (e `http://localhost:5207`).

### 4. Front-end

Abra a pasta `front-end` com um servidor estático (ex.: Live Server) e acesse o `index.html`. Abrir o arquivo direto pelo `file://` não é recomendado.

---

## Endpoints da API

| Método | Rota | Descrição | Respostas |
|---|---|---|---|
| `GET` | `/lembretes` | Lista todos os lembretes ativos | 200 |
| `GET` | `/lembretes/{id}` | Busca um lembrete ativo pelo id | 200 · 400 · 404 |
| `GET` | `/lembretes/filtro` | Lista com filtros, ordenação e paginação | 200 · 400 |
| `GET` | `/lembretes/imprimir` | Gera o relatório em PDF com os mesmos filtros | 200 · 400 |
| `POST` | `/lembretes` | Cria um lembrete | 201 · 400 |
| `PUT` | `/lembretes/{id}` | Atualiza a descrição | 200 · 400 · 404 |
| `DELETE` | `/lembretes/{id}` | Exclusão lógica (`IsAtivo = false`) | 204 · 400 · 404 |

### Parâmetros de `/lembretes/filtro` e `/lembretes/imprimir`

| Parâmetro | Tipo | Exemplo |
|---|---|---|
| `NumeroPagina` | int (≥ 1) | `1` |
| `TamanhoPagina` | int (1 a 100) | `10` |
| `Ordem` | `Descricao` · `DataCadastro` · `DataAtualizacao` · `IsAtivo` | `DataCadastro` |
| `Direcao` | `ASC` · `DESC` | `DESC` |
| `Descricao` | texto (busca parcial) | `blink` |
| `DataCadastroInicial` / `DataCadastroFinal` | data | `2026-10-01` |
| `DataAtualizacaoInicial` / `DataAtualizacaoFinal` | data | `2026-10-31` |

Como o endpoint usa `[AsParameters]`, todos os parâmetros são obrigatórios na query string. Quando um filtro de data não é preenchido, o front-end envia `1900-01-01` (inicial) e `2999-12-31` (final), que funcionam como "sem limite".

Exemplo:

```
GET /lembretes/filtro?NumeroPagina=1&TamanhoPagina=10&Ordem=DataCadastro&Direcao=DESC&Descricao=&DataCadastroInicial=1900-01-01&DataCadastroFinal=2999-12-31&DataAtualizacaoInicial=1900-01-01&DataAtualizacaoFinal=2999-12-31
```

Resposta:

```json
{
  "lembretes": [ { "id": "…", "descricao": "Comprar Black King Bar", "dataCadastro": "…", "dataAtualizacao": "…", "isAtivo": true } ],
  "total": 12,
  "numeroPagina": 1,
  "tamanhoPagina": 10
}
```

---

## Comunicação entre front-end e API

Todas as chamadas passam pela função `requisitar()` em `js/app.js`, que usa `fetch` e padroniza os erros:

- **Sem resposta** (API desligada, certificado não confiável ou CORS bloqueado): *"Não foi possível conectar à API. Verifique se ela está em execução."*
- **400**: mostra a mensagem de validação devolvida pela API (ex.: *"A descrição deve ter no máximo 500 caracteres."*).
- **404**: *"Lembrete não encontrado. Ele pode ter sido excluído."*
- **500**: *"Erro interno na API. Tente novamente em instantes."*

Os parâmetros do filtro são montados com `URLSearchParams` e registrados no console (`filtro-> params`), o que facilita conferir as requisições na aba **Network** do DevTools.

---

## CORS

A API registra a política `Frontend`:

```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

app.UseCors("Frontend");
```

Como o front-end roda em outra origem (ex.: `http://127.0.0.1:5500`), o navegador envia uma requisição **preflight `OPTIONS`** antes de `POST`, `PUT` e `DELETE` com JSON. A API responde `204` com `Access-Control-Allow-Origin: *`, liberando a chamada real. Em produção, o ideal é restringir com `.WithOrigins("https://dominio-do-front")`.

---

## Funcionalidades do front-end

- **CRUD completo:** criar, editar, buscar por id e excluir lembretes.
- **Filtros:** descrição, data de cadastro (de/até) e data de atualização (de/até).
- **Paginação:** primeira, anterior, números de página com reticências, próxima e última.
- **Tamanho da página:** 5, 10, 20 ou 50 registros.
- **Ordenação:** clicando no cabeçalho da coluna. Clicar de novo inverte a direção, e a seta indica a ordem atual (`aria-sort`).
- **Mensagens:**
  - Sucesso ao criar, editar e excluir.
  - Campo obrigatório, com destaque no campo e um contador de 0 a 500 caracteres.
  - Intervalo de datas inválido (data inicial maior que a final).
  - Estado de **lista vazia**.
  - Estado de **erro de comunicação**, com o botão **"Tentar novamente"**.
  - Mensagens rápidas no canto da tela (toasts) para sucesso, aviso e erro.
- **Modal de confirmação** antes de excluir, que fecha por Cancelar, pela tecla Esc ou com clique fora.
- **Imprimir:** baixa o relatório em PDF respeitando os filtros, a ordenação e a página atuais.
- **Segurança:** as descrições são escapadas antes de ir para o HTML, então um texto como `<img onerror=…>` aparece como texto e não é executado.
- **Responsivo:** o layout se adapta até 375 px sem rolagem horizontal na página.

### Tema Dota 2

| Elemento | Cor |
|---|---|
| Fundo (pedra escura) | `#0A0C0F` / `#171B21` |
| Vermelho Dire (ações principais) | `#B8321F` · `#E0472F` |
| Dourado (títulos e destaques) | `#C9A45C` |
| Verde Radiant (status ativo) | `#8FD15B` |

Tipografia: **Cinzel** nos títulos, **Barlow Condensed** nos rótulos e botões e **Barlow** no texto.

---

## Relatório em PDF

Gerado pela API com **QuestPDF**, seguindo o mesmo tema do front-end:

- Página A4 escura, com cabeçalho repetido em todas as folhas: emblema, título "Lembretes dos Ancestrais", subtítulo "RELATÓRIO DE MISSÕES" e linha em degradê vermelho.
- Indicadores: total encontrado, registros nesta folha, página da listagem e ordenação usada.
- Linha com os filtros aplicados, em texto legível (ex.: "cadastro a partir de 01/10/2026").
- Tabela com linhas alternadas e o status como selo **ATIVO** / **INATIVO**.
- Rodapé "UMFG · PROGRAMAÇÃO III" com a numeração "FOLHA X DE Y".
- As fontes Cinzel e Barlow ficam na pasta `Fontes/` e são copiadas para a saída do build. Para emoji e símbolos que elas não têm, o relatório usa fontes do sistema como reserva.

---

## O que foi alterado

### API (`Program.cs`, `Entidades/Lembrete.cs`, `.csproj`)

| Alteração | Antes | Depois |
|---|---|---|
| Ordenação | `OrderBy(x => dto.Ordem)` ordenava por uma string constante, ou seja, não ordenava | `switch` por campo (`Descricao`, `DataCadastro`, `DataAtualizacao`, `IsAtivo`) e direção `ASC`/`DESC` |
| Codificação do arquivo | `Program.cs` em cp1252 ("Relat�rio") | Convertido para UTF-8 |
| Relatório PDF | Layout simples | Layout novo no tema Dota 2, com fontes próprias registradas via `FontManager` |
| Lógica do filtro | Duplicada nos endpoints de filtro e de impressão | Extraída para o método `FiltrarLembretes` |
| Validação de descrição (POST/PUT) | Descrição vazia ou com mais de 500 caracteres gerava **500** | Retorna **400** com mensagem |
| **PUT com descrição vazia** | Era gravado no banco e fazia **todas** as consultas seguintes falharem com 500 | Recusado com 400, e a entidade também passou a rejeitar descrição vazia |
| POST | Recebia a entidade `Lembrete` direto no corpo | Recebe `LembreteDTO`, valida e só então cria a entidade |
| Id inválido (`/lembretes/abc`) | `Guid.Parse` lançava exceção → 500 | `Guid.TryParse` → 400 |
| PUT/DELETE em lembrete já excluído | Editava ou "excluía" de novo | Retorna 404 |
| Paginação | Página ou tamanho enormes estouravam o cálculo do `Skip` → 500 | Validação centralizada: tamanho máximo 100 e proteção contra estouro → 400 |
| PDF com emoji ou símbolos | QuestPDF lançava exceção por caractere ausente → 500 | Fontes de reserva do sistema; o PDF é gerado normalmente |
| Data final extrema (`9999-12-31`) | `AddDays(1)` estourava o limite do `DateTime` → 500 | A comparação é feita só pela data, sem estouro |
| Arquivo `.http` | Exemplo do template (`/weatherforecast`), rota inexistente | Exemplos reais: listar, filtrar e criar lembretes |
| `.csproj` | — | Copia `Fontes/*.ttf` para a pasta de saída |

### Front-end (`index.html`, `css/style.css`, `js/app.js`)

- Interface reescrita no tema Dota 2, responsiva.
- Implementados todos os filtros, a paginação, o tamanho de página e a ordenação clicando no cabeçalho.
- Função `requisitar()` centralizando o `fetch` e o tratamento de erros por status.
- A mensagem de validação da API é mostrada nos erros 400.
- Estados de carregando, lista vazia e erro com "Tentar novamente".
- Validação de campo obrigatório, contador de caracteres e validação de intervalo de datas.
- Modal de confirmação de exclusão no lugar do `confirm()` nativo.
- Toasts de sucesso, aviso e erro.
- Escape de HTML nas descrições.
- Ao excluir o último item da última página, a tela volta automaticamente para a página anterior.
- Cliques rápidos em ordenar ou paginar não mostram mais um resultado desatualizado: só a última consulta é exibida.
- Com a API fora do ar, a paginação é zerada, sem botões de próxima página ativos para uma lista vazia.

---

## Testes realizados

### API: 42 cenários, todos aprovados

- CRUD feliz: POST 201, GET por id 200, PUT 200, GET todos 200, DELETE 204.
- POST com descrição vazia, só com espaços, ausente, JSON inválido e 501 caracteres → 400. Com exatamente 500 caracteres → 201.
- GET, PUT e DELETE com id inválido → 400. Com id inexistente → 404.
- PUT com descrição vazia ou com 501 caracteres → 400.
- Filtro: página 0, tamanho 0, tamanho negativo, data inválida, página enorme e tamanho enorme → 400. Direção ou ordem inválidas e direção em minúsculo → 200, com valor padrão. Caracteres especiais do `LIKE` (`%`, `_`, `\`) → 200.
- PDF: padrão, com filtros, sem resultados e com emoji → 200 `application/pdf`. Página 0 → 400.
- Datas finais extremas (`9999-12-31`) no filtro e no PDF → 200.
- CORS: preflight `OPTIONS` → 204 com `Access-Control-Allow-Origin: *`.
- Depois de excluir: GET, PUT e um segundo DELETE → 404.

### Front-end: 38 cenários, todos aprovados

- Campo obrigatório, contador, criar, editar e escape de HTML.
- Mensagem de 400 vinda da API (descrição longa enviada sem passar pelo `maxlength`).
- Filtro por descrição, lista vazia, limpar filtros, intervalo de datas inválido e filtro só com data inicial.
- Ordenação crescente e decrescente, e cliques rápidos seguidos.
- Paginação: tamanho 5, próxima, última, número de página, botões desabilitados e volta automática ao excluir o último item da página.
- Modal: cancelar, Esc, clique fora e confirmar. Excluir o lembrete que está aberto em edição fecha o formulário.
- Editar um lembrete que já foi excluído → mensagem de "não encontrado".
- Imprimir com sucesso, e listar, criar e imprimir com a API desligada, seguido de "Tentar novamente".
- Layout em 375 px sem rolagem horizontal.

---

## Créditos e licenças

- Projeto base: repositórios da disciplina de Programação III, UMFG.
- Fontes [Cinzel](https://github.com/NDISCOVER/Cinzel) e [Barlow](https://github.com/jpt/barlow), sob a **SIL Open Font License 1.1**. Os textos das licenças estão em `Fontes/OFL-Cinzel.txt` e `Fontes/OFL-Barlow.txt`.
- [QuestPDF](https://www.questpdf.com/) usado sob a licença Community.
- *Dota 2* é marca registrada da Valve Corporation. Este projeto é acadêmico, sem fins comerciais e sem vínculo com a Valve; o tema é apenas uma inspiração visual (cores e tipografia) e não usa nenhum recurso oficial do jogo.
