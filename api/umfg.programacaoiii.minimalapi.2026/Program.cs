using Microsoft.EntityFrameworkCore;
using QuestPDF;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using umfg.programacaoiii.minimalapi._2026.Contexto;
using umfg.programacaoiii.minimalapi._2026.DTO;
using umfg.programacaoiii.minimalapi._2026.Entidades;

namespace umfg.programacaoiii.minimalapi._2026;

public class Program
{    
    //método de startup do projeto
    public static void Main(string[] args)
    {
        //variavel padrão do .net permite definir as caracteristicas que a api irá ter
        //ex: autenticação e autorização via JWT / qual banco de dados
        var builder = WebApplication.CreateBuilder(args);
        var connectionString = builder.Configuration["ConnectionString"] ?? string.Empty;

        //aqui configuramos a conexão da API com o banco de dados
        builder.Services
            .AddDbContext<MySqlContexto>(options => options.UseMySQL(connectionString));
            //.AddDbContext<MySqlContexto>(options => options.UseMySQL("Server=localhost;Port=3308;Database=umfg_lembrete_develop;Uid=root;Pwd=root;"));

        //implementacao do CORS para permitir que a aplicação frontend acesse a API (localhost)
        //no mundo real, restrinja o acesso apenas para o dominio do frontend conhecido. Ex.: .WithOrigins("https://app.empresa.com.br")
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("Frontend", policy =>
            {
                policy
                    .AllowAnyOrigin()
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            });
        });

        //aqui a aplicação é construída
        var app = builder.Build();

        app.MapPost("/lembretes", async (LembreteDTO dto, MySqlContexto contexto) =>
        {
            //valida antes de criar a entidade, senão o construtor lança exceção e a API devolve 500
            if (ValidarDescricao(dto.Descricao) is string erro)
                return Results.BadRequest(erro);

            var lembrete = new Lembrete(dto.Descricao.Trim());

            await contexto.Lembrete.AddAsync(lembrete);
            await contexto.SaveChangesAsync();

            return Results.Created($"/lembretes/{lembrete.Id}", lembrete);
        });

        app.MapGet("/lembretes/{id}", async (string id, MySqlContexto contexto) =>
        {
            if (!Guid.TryParse(id, out var guid))
                return Results.BadRequest("id invalido!");

            return await contexto.Lembrete.FirstOrDefaultAsync(x => x.Id == guid && x.IsAtivo)
            is Lembrete lembrete ? Results.Ok(lembrete) : Results.NotFound();
        });

        app.MapGet("/lembretes", async (MySqlContexto contexto) =>
        {
            return await contexto.Lembrete.Where(x => x.IsAtivo).ToListAsync();            
        });

        app.MapGet("/lembretes/filtro", async ([AsParameters] LembreteQueryDTO.LembreteQueryRequestDTO dto, MySqlContexto contexto) =>
        {
            if (ValidarPaginacao(dto) is string erro)
                return Results.BadRequest(erro);

            var (lembretes, total) = await FiltrarLembretes(dto, contexto);

            var resultado = new LembreteQueryDTO.LembreteQueryResponseDTO()
            {
                Total = total,
                NumeroPagina = dto.NumeroPagina,
                TamanhoPagina = dto.TamanhoPagina,
                Lembretes = lembretes,
            };

            return Results.Ok(resultado);
        });

        //Biblioteca -> QuestPDF
        Settings.License = LicenseType.Community;

        //a descrição é texto livre (emoji, símbolos, outros alfabetos): usa as fontes do sistema como reserva
        //e, se mesmo assim faltar algum caractere, desenha um espaço em vez de derrubar a geração do PDF
        Settings.UseSystemFonts = true;
        Settings.ThrowOnMissingTextGlyphs = false;
        Settings.ThrowOnMissingFontFamilies = false;

        //fontes do tema Dota 2 (Cinzel e Barlow, licença OFL) copiadas para a pasta de saída
        FontManager.RegisterFontsFromDirectory(Path.Combine(AppContext.BaseDirectory, "Fontes"));

        app.MapGet("/lembretes/imprimir", async ([AsParameters] LembreteQueryDTO.LembreteQueryRequestDTO dto, MySqlContexto contexto) =>
        {
            if (ValidarPaginacao(dto) is string erro)
                return Results.BadRequest(erro);

            var (lembretes, total) = await FiltrarLembretes(dto, contexto);

            return Results.File(GerarRelatorioPdf(dto, lembretes, total), "application/pdf", "relatorio-lembretes.pdf");
        });


        app.MapDelete("/lembretes/{id}", async (string id, MySqlContexto contexto) =>
        {
            if (!Guid.TryParse(id, out var guid))
                return Results.BadRequest("id invalido!");

            var lembrete = await contexto.Lembrete.FindAsync(guid);

            //conceito de fail first (lembrete já excluído também conta como não encontrado)
            if (lembrete is null || !lembrete.IsAtivo)
                return Results.NotFound();

            lembrete.SetAtivo(false);
            lembrete.Update();

            contexto.Lembrete.Update(lembrete);
            await contexto.SaveChangesAsync();

            return Results.NoContent();
        });

        app.MapPut("/lembretes/{id}", async (string id, LembreteDTO dto, MySqlContexto contexto) =>
        {
            if (!Guid.TryParse(id, out var guid))
                return Results.BadRequest("id invalido!");

            if (ValidarDescricao(dto.Descricao) is string erro)
                return Results.BadRequest(erro);

            var lembreteExistente = await contexto.Lembrete.FindAsync(guid);

            if (lembreteExistente is null || !lembreteExistente.IsAtivo)
                return Results.NotFound();

            lembreteExistente.SetDescricao(dto.Descricao.Trim());
            lembreteExistente.Update();

            contexto.Lembrete.Update(lembreteExistente);
            await contexto.SaveChangesAsync();

            return Results.Ok(lembreteExistente);
        });

        //aqui habilitamos as funcionalidades da api
        app.UseHttpsRedirection();

        //habilitar o uso da regra de CORS
        app.UseCors("Frontend");

        //a api é iniciada de fato
        app.Run();
        //tudo que for colocado após o run será executado somente quando a api for parada
    }

    private const int C_TAMANHO_MAXIMO_DESCRICAO = 500;
    private const int C_TAMANHO_MAXIMO_PAGINA = 100;

    //retorna a mensagem de erro ou null quando a descrição é válida
    private static string? ValidarDescricao(string? descricao)
    {
        if (string.IsNullOrWhiteSpace(descricao))
            return "A descrição é obrigatória.";

        if (descricao.Trim().Length > C_TAMANHO_MAXIMO_DESCRICAO)
            return $"A descrição deve ter no máximo {C_TAMANHO_MAXIMO_DESCRICAO} caracteres.";

        return null;
    }

    private static string? ValidarPaginacao(LembreteQueryDTO.LembreteQueryRequestDTO dto)
    {
        if (dto.NumeroPagina <= 0 || dto.TamanhoPagina <= 0)
            return "Página e tamanho da página são obrigatórios.";

        if (dto.TamanhoPagina > C_TAMANHO_MAXIMO_PAGINA)
            return $"O tamanho da página deve ser no máximo {C_TAMANHO_MAXIMO_PAGINA}.";

        //evita estouro de int no cálculo do Skip
        if ((long)(dto.NumeroPagina - 1) * dto.TamanhoPagina > int.MaxValue)
            return "Número da página inválido.";

        return null;
    }

    private static async Task<(List<Lembrete>, long)>FiltrarLembretes(
        LembreteQueryDTO.LembreteQueryRequestDTO dto, MySqlContexto contexto)
    {
        var query = contexto.Lembrete
                .AsNoTracking()
                .Where(x => x.IsAtivo);

        //busca por descrição com tratamento de caracteres especiais para LIKE
        if (!string.IsNullOrWhiteSpace(dto.Descricao))
        {
            var termo = dto.Descricao.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
            query = query.Where(x => EF.Functions.Like(x.Descricao, $"%{termo}%"));
        }

        // Filtros opcionais
        if (dto.DataCadastroInicial > DateTime.MinValue)
            query = query.Where(x => x.DataCadastro >= dto.DataCadastroInicial);

        //compara só a data: no último dia possível (31/12/9999) o AddDays(1) estouraria
        if (dto.DataCadastroFinal.Date < DateTime.MaxValue.Date)
            query = query.Where(x => x.DataCadastro < dto.DataCadastroFinal.Date.AddDays(1)); // inclui o dia inteiro

        if (dto.DataAtualizacaoInicial > DateTime.MinValue)
            query = query.Where(x => x.DataAtualizacao >= dto.DataAtualizacaoInicial);

        if (dto.DataAtualizacaoFinal.Date < DateTime.MaxValue.Date)
            query = query.Where(x => x.DataAtualizacao < dto.DataAtualizacaoFinal.Date.AddDays(1)); // inclui o dia inteiro

        //direção inválida volta para o padrão: do mais recente para o mais antigo
        if (dto.Direcao.ToUpper() != "ASC" && dto.Direcao.ToUpper() != "DESC")
            dto.Direcao = "DESC";

        //ordenação
        //ordena pelo campo informado (Descricao, DataCadastro, DataAtualizacao, IsAtivo)
        var desc = dto.Direcao.ToUpper() == "DESC";
        query = dto.Ordem switch
        {
            "Descricao" => desc ? query.OrderByDescending(x => x.Descricao) : query.OrderBy(x => x.Descricao),
            "DataAtualizacao" => desc ? query.OrderByDescending(x => x.DataAtualizacao) : query.OrderBy(x => x.DataAtualizacao),
            "IsAtivo" => desc ? query.OrderByDescending(x => x.IsAtivo) : query.OrderBy(x => x.IsAtivo),
            _ => desc ? query.OrderByDescending(x => x.DataCadastro) : query.OrderBy(x => x.DataCadastro),
        };

        // paginação
        var pular = (dto.NumeroPagina - 1) * dto.TamanhoPagina;
        var lembretes = await query.Skip(pular).Take(dto.TamanhoPagina).ToListAsync();
        var total = await query.CountAsync();

        return (lembretes, total);
    }

    //paleta do tema Dota 2 (mesmas cores do front-end)
    private const string C_COR_FUNDO = "#12161B";
    private const string C_COR_PAINEL = "#1B1F26";
    private const string C_COR_PAINEL_2 = "#171B21";
    private const string C_COR_BORDA = "#2C323B";
    private const string C_COR_TEXTO = "#D9D4C7";
    private const string C_COR_TEXTO_CLARO = "#F1ECE0";
    private const string C_COR_TEXTO_SUAVE = "#8B919B";
    private const string C_COR_VERMELHO = "#B8321F";
    private const string C_COR_VERMELHO_CLARO = "#E0472F";
    private const string C_COR_VERMELHO_ESCURO = "#7A1E12";
    private const string C_COR_DOURADO = "#C9A45C";
    private const string C_COR_DOURADO_CLARO = "#E6C47C";
    private const string C_COR_RADIANT = "#8FD15B";

    private const string C_FONTE_TITULO = "Cinzel";
    private const string C_FONTE_ROTULO = "Barlow Condensed";
    private const string C_FONTE_TEXTO = "Barlow";

    //fontes do Windows usadas só para caracteres que a Barlow não tem
    private static readonly string[] C_FONTES_RESERVA = ["Segoe UI Symbol", "Segoe UI Emoji", "Microsoft YaHei"];

    private const string C_EMBLEMA_SVG = """
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64">
            <path fill="#B8321F" d="M10 6 L54 6 L58 10 L58 54 L54 58 L10 58 L6 54 L6 10 Z"/>
            <path fill="#12161B" d="M16 14 L26 14 L50 46 L50 50 L40 50 L14 18 Z"/>
            <path fill="#12161B" d="M14 36 L24 50 L14 50 Z"/>
            <path fill="#12161B" d="M40 14 L50 14 L50 28 Z"/>
        </svg>
        """;

    private static byte[] GerarRelatorioPdf(
        LembreteQueryDTO.LembreteQueryRequestDTO dto, List<Lembrete> lembretes, long total)
    {
        var geradoEm = DateTime.Now.ToString("dd/MM/yyyy 'às' HH:mm");

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.5f, Unit.Centimetre);
                page.PageColor(C_COR_FUNDO);
                page.DefaultTextStyle(x => x.FontFamily([C_FONTE_TEXTO, .. C_FONTES_RESERVA]).FontSize(10).FontColor(C_COR_TEXTO));

                //cabeçalho repetido em todas as folhas
                page.Header().Column(cabecalho =>
                {
                    cabecalho.Item().Row(linha =>
                    {
                        linha.ConstantItem(42).Height(42).Svg(C_EMBLEMA_SVG);

                        linha.RelativeItem().PaddingLeft(12).Column(titulo =>
                        {
                            titulo.Item().Text("Lembretes dos Ancestrais")
                                .FontFamily(C_FONTE_TITULO).Black().FontSize(22).FontColor(C_COR_TEXTO_CLARO);

                            titulo.Item().Text("RELATÓRIO DE MISSÕES")
                                .FontFamily(C_FONTE_ROTULO).Bold().FontSize(10).LetterSpacing(0.15f).FontColor(C_COR_DOURADO);
                        });

                        linha.AutoItem().AlignBottom().AlignRight().Text($"Gerado em {geradoEm}")
                            .FontSize(9).FontColor(C_COR_TEXTO_SUAVE);
                    });

                    cabecalho.Item().PaddingTop(10).Height(2)
                        .BackgroundLinearGradient(0, [C_COR_VERMELHO_CLARO, C_COR_VERMELHO_ESCURO, C_COR_FUNDO]);
                });

                page.Content().PaddingVertical(16).Column(conteudo =>
                {
                    conteudo.Spacing(14);

                    //quadro de resumo
                    conteudo.Item().Row(resumo =>
                    {
                        resumo.Spacing(10);

                        Indicador(resumo.RelativeItem(), "Total encontrado", total.ToString());
                        Indicador(resumo.RelativeItem(), "Nesta folha", lembretes.Count.ToString());
                        Indicador(resumo.RelativeItem(), "Página da listagem", dto.NumeroPagina.ToString());
                        Indicador(resumo.RelativeItem(2), "Ordenação", DescreverOrdenacao(dto));
                    });

                    conteudo.Item().Text(texto =>
                    {
                        texto.Span("FILTROS  ").FontFamily(C_FONTE_ROTULO).Bold().FontSize(9).LetterSpacing(0.1f).FontColor(C_COR_DOURADO);
                        texto.Span(DescreverFiltros(dto)).FontSize(9).FontColor(C_COR_TEXTO_SUAVE);
                    });

                    //tabela de lembretes
                    conteudo.Item().Border(1).BorderColor(C_COR_BORDA).Table(tabela =>
                    {
                        tabela.ColumnsDefinition(colunas =>
                        {
                            colunas.RelativeColumn();
                            colunas.ConstantColumn(92);
                            colunas.ConstantColumn(92);
                            colunas.ConstantColumn(62);
                        });

                        tabela.Header(header =>
                        {
                            foreach (var titulo in new[] { "Descrição", "Cadastro", "Atualização", "Status" })
                            {
                                header.Cell()
                                    .BackgroundLinearGradient(90, ["#252B34", C_COR_PAINEL])
                                    .BorderBottom(2).BorderColor(C_COR_VERMELHO_ESCURO)
                                    .PaddingVertical(8).PaddingHorizontal(10)
                                    .Text(titulo.ToUpper())
                                    .FontFamily(C_FONTE_ROTULO).Bold().FontSize(9).LetterSpacing(0.12f).FontColor(C_COR_TEXTO_SUAVE);
                            }
                        });

                        var indice = 0;

                        foreach (var lembrete in lembretes)
                        {
                            var fundo = indice++ % 2 == 0 ? C_COR_PAINEL_2 : C_COR_PAINEL;

                            IContainer Celula() => tabela.Cell()
                                .Background(fundo)
                                .BorderBottom(0.5f).BorderColor(C_COR_BORDA)
                                .PaddingVertical(7).PaddingHorizontal(10)
                                .AlignMiddle();

                            Celula().Text(lembrete.Descricao).FontColor(C_COR_TEXTO_CLARO);
                            Celula().Text(lembrete.DataCadastro.ToString("dd/MM/yyyy HH:mm")).FontSize(9).FontColor(C_COR_TEXTO_SUAVE);
                            Celula().Text(lembrete.DataAtualizacao.ToString("dd/MM/yyyy HH:mm")).FontSize(9).FontColor(C_COR_TEXTO_SUAVE);
                            Status(Celula(), lembrete.IsAtivo);
                        }
                    });

                    if (lembretes.Count == 0)
                    {
                        conteudo.Item().PaddingVertical(20).AlignCenter()
                            .Text("Nenhum lembrete encontrado.")
                            .FontFamily(C_FONTE_ROTULO).Bold().FontSize(12).FontColor(C_COR_TEXTO_SUAVE);
                    }
                });

                page.Footer().BorderTop(1).BorderColor(C_COR_BORDA).PaddingTop(8).Row(rodape =>
                {
                    rodape.RelativeItem().Text("UMFG · PROGRAMAÇÃO III")
                        .FontFamily(C_FONTE_ROTULO).Bold().FontSize(8).LetterSpacing(0.12f).FontColor(C_COR_TEXTO_SUAVE);

                    rodape.RelativeItem().AlignRight().Text(texto =>
                    {
                        texto.DefaultTextStyle(x => x.FontFamily(C_FONTE_ROTULO).Bold().FontSize(9).LetterSpacing(0.1f).FontColor(C_COR_DOURADO));
                        texto.Span("FOLHA ");
                        texto.CurrentPageNumber();
                        texto.Span(" DE ");
                        texto.TotalPages();
                    });
                });
            });
        }).GeneratePdf();
    }

    private static void Indicador(IContainer container, string rotulo, string valor)
    {
        container
            .Background(C_COR_PAINEL)
            .BorderLeft(3).BorderColor(C_COR_VERMELHO)
            .PaddingVertical(8).PaddingHorizontal(10)
            .Column(coluna =>
            {
                coluna.Item().Text(rotulo.ToUpper())
                    .FontFamily(C_FONTE_ROTULO).Bold().FontSize(8).LetterSpacing(0.12f).FontColor(C_COR_TEXTO_SUAVE);

                coluna.Item().PaddingTop(2).Text(valor)
                    .FontFamily(C_FONTE_TITULO).Bold().FontSize(12).FontColor(C_COR_DOURADO_CLARO);
            });
    }

    private static void Status(IContainer container, bool isAtivo)
    {
        container
            .AlignLeft()
            .Background(isAtivo ? "#1F2D1A" : "#2E1612")
            .Border(0.75f).BorderColor(isAtivo ? "#3F6A28" : "#6B2219")
            .PaddingVertical(2).PaddingHorizontal(6)
            .Text(isAtivo ? "ATIVO" : "INATIVO")
            .FontFamily(C_FONTE_ROTULO).Bold().FontSize(8).LetterSpacing(0.1f)
            .FontColor(isAtivo ? C_COR_RADIANT : "#FF7A63");
    }

    private static string DescreverOrdenacao(LembreteQueryDTO.LembreteQueryRequestDTO dto)
    {
        var campo = dto.Ordem switch
        {
            "Descricao" => "Descrição",
            "DataAtualizacao" => "Atualização",
            "IsAtivo" => "Status",
            _ => "Cadastro",
        };

        return $"{campo} {(dto.Direcao.ToUpper() == "ASC" ? "cresc." : "decresc.")}";
    }

    //o front-end envia 1900-01-01 e 2999-12-31 quando a data não foi preenchida
    private static string DescreverFiltros(LembreteQueryDTO.LembreteQueryRequestDTO dto)
    {
        static bool Informada(DateTime data) => data.Year > 1900 && data.Year < 2999;

        static string Intervalo(string nome, DateTime inicial, DateTime final)
        {
            if (!Informada(inicial) && !Informada(final))
                return string.Empty;

            if (!Informada(final))
                return $"{nome} a partir de {inicial:dd/MM/yyyy}";

            if (!Informada(inicial))
                return $"{nome} até {final:dd/MM/yyyy}";

            return $"{nome} de {inicial:dd/MM/yyyy} até {final:dd/MM/yyyy}";
        }

        var filtros = new List<string>();

        if (!string.IsNullOrWhiteSpace(dto.Descricao))
            filtros.Add($"descrição contém \"{dto.Descricao}\"");

        filtros.Add(Intervalo("cadastro", dto.DataCadastroInicial, dto.DataCadastroFinal));
        filtros.Add(Intervalo("atualização", dto.DataAtualizacaoInicial, dto.DataAtualizacaoFinal));
        filtros.RemoveAll(string.IsNullOrEmpty);

        return filtros.Count == 0 ? "nenhum filtro aplicado" : string.Join(" · ", filtros);
    }
}