const API_URL = "https://localhost:7063";

const DATA_MINIMA = "1900-01-01";
const DATA_MAXIMA = "2999-12-31";

let paginaAtual = 1;
let totalPaginas = 1;
let ordemAtual = "DataCadastro";
let direcaoAtual = "DESC";
let ultimaConsulta = 0;

const formulario = document.getElementById("formulario");
const formLembrete = document.getElementById("formLembrete");

const lembreteId = document.getElementById("lembreteId");
const descricao = document.getElementById("descricao");
const erroDescricao = document.getElementById("erroDescricao");
const contadorDescricao = document.getElementById("contadorDescricao");

const listaLembretes = document.getElementById("listaLembretes");
const carregando = document.getElementById("carregando");
const mensagemVazia = document.getElementById("mensagemVazia");
const mensagemErro = document.getElementById("mensagemErro");
const mensagemErroTexto = document.getElementById("mensagemErroTexto");

const formFiltro = document.getElementById("formFiltro");
const filtroDescricao = document.getElementById("filtroDescricao");
const filtroCadastroInicial = document.getElementById("filtroCadastroInicial");
const filtroCadastroFinal = document.getElementById("filtroCadastroFinal");
const filtroAtualizacaoInicial = document.getElementById("filtroAtualizacaoInicial");
const filtroAtualizacaoFinal = document.getElementById("filtroAtualizacaoFinal");
const tamanhoPagina = document.getElementById("tamanhoPagina");

const totalRegistros = document.getElementById("totalRegistros");
const paginaAtualElemento = document.getElementById("paginaAtual");
const paginas = document.getElementById("paginas");

const btnImprimir = document.getElementById("btnImprimir");
const btnNovo = document.getElementById("btnNovo");
const btnCancelar = document.getElementById("btnCancelar");
const btnSalvar = document.getElementById("btnSalvar");
const btnLimpar = document.getElementById("btnLimpar");
const btnTentarNovamente = document.getElementById("btnTentarNovamente");

const btnPrimeira = document.getElementById("btnPrimeira");
const btnAnterior = document.getElementById("btnAnterior");
const btnProximo = document.getElementById("btnProximo");
const btnUltima = document.getElementById("btnUltima");

const cabecalhosOrdenaveis = document.querySelectorAll("th.sortable");

const tituloFormulario = document.getElementById("tituloFormulario");

const modalConfirmacao = document.getElementById("modalConfirmacao");
const textoModal = document.getElementById("textoModal");
const btnModalCancelar = document.getElementById("btnModalCancelar");
const btnModalConfirmar = document.getElementById("btnModalConfirmar");

document.addEventListener("DOMContentLoaded", () => {
    atualizarIndicadoresOrdenacao();
    filtrarLembretes();
});


async function requisitar(caminho, opcoes = {}) {
    let response;

    try {
        response = await fetch(`${API_URL}${caminho}`, opcoes);
    } catch (error) {
        // fetch só lança exceção quando não há resposta: API fora do ar,
        // certificado não confiável ou bloqueio de CORS
        throw new Error("Não foi possível conectar à API. Verifique se ela está em execução.");
    }

    if (!response.ok) {
        const mensagemApi = response.status === 400 ? await lerMensagemApi(response) : "";
        throw new Error(mensagemApi || mensagemPorStatus(response.status));
    }

    return response;
}

// a API devolve a validação como texto (ex.: "A descrição é obrigatória.")
async function lerMensagemApi(response) {
    try {
        const corpo = await response.json();
        return typeof corpo === "string" ? corpo : "";
    } catch {
        return "";
    }
}

function mensagemPorStatus(status) {
    switch (status) {
        case 400: return "Requisição inválida. Verifique os dados informados.";
        case 404: return "Lembrete não encontrado. Ele pode ter sido excluído.";
        case 500: return "Erro interno na API. Tente novamente em instantes.";
        default: return `A API respondeu com erro (HTTP ${status}).`;
    }
}

function montarParametros() {
    const params = new URLSearchParams();

    params.append("NumeroPagina", paginaAtual);
    params.append("TamanhoPagina", tamanhoPagina.value);
    params.append("Ordem", ordemAtual);
    params.append("Direcao", direcaoAtual);
    params.append("Descricao", filtroDescricao.value.trim());
    params.append("DataCadastroInicial", filtroCadastroInicial.value || DATA_MINIMA);
    params.append("DataCadastroFinal", filtroCadastroFinal.value || DATA_MAXIMA);
    params.append("DataAtualizacaoInicial", filtroAtualizacaoInicial.value || DATA_MINIMA);
    params.append("DataAtualizacaoFinal", filtroAtualizacaoFinal.value || DATA_MAXIMA);

    return params;
}

async function filtrarLembretes() {
    // cliques rápidos (ordenar, paginar) disparam várias consultas; só a última vale
    const consulta = ++ultimaConsulta;

    mostrarEstado(carregando);

    try {
        const params = montarParametros();

        console.log("filtro-> params", params.toString());

        const response = await requisitar(`/lembretes/filtro?${params}`);
        const resultado = await response.json();

        if (consulta !== ultimaConsulta) {
            return;
        }

        totalPaginas = Math.max(1, Math.ceil(resultado.total / resultado.tamanhoPagina));

        // ex.: excluiu o último registro da última página
        if (paginaAtual > totalPaginas) {
            paginaAtual = totalPaginas;
            return filtrarLembretes();
        }

        renderizarLembretes(resultado.lembretes);
        renderizarPaginacao(resultado.total);

    } catch (error) {

        if (consulta !== ultimaConsulta) {
            return;
        }

        console.error(error);

        listaLembretes.innerHTML = "";
        mensagemErroTexto.textContent = error.message;
        mostrarEstado(mensagemErro);

        // sem resposta não há páginas para navegar; "Tentar novamente" refaz a consulta da página atual
        totalPaginas = 1;
        paginaAtual = 1;
        renderizarPaginacao(0);

        mostrarToast(error.message, "erro");
    }
}


async function imprimirLembretes() {
    if (!validarFiltros()) {
        return;
    }

    btnImprimir.disabled = true;

    try {
        const params = montarParametros();

        const response = await requisitar(`/lembretes/imprimir?${params}`);

        const blob = await response.blob();
        const url = URL.createObjectURL(blob);

        const a = document.createElement("a");
        a.href = url;
        a.download = "relatorio-lembretes.pdf";

        document.body.appendChild(a);
        a.click();
        a.remove();

        setTimeout(() => URL.revokeObjectURL(url), 10000);

        mostrarToast("Relatório gerado com sucesso!", "sucesso");

    } catch (error) {

        console.error(error);

        mostrarToast(`Não foi possível gerar o relatório. ${error.message}`, "erro");

    } finally {
        btnImprimir.disabled = false;
    }
}


async function criarLembrete() {
    try {

        const dados = {
            descricao: descricao.value.trim()
        };

        await requisitar("/lembretes", {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify(dados)
        });

        mostrarToast("Lembrete criado com sucesso!", "sucesso");

        fecharFormulario();

        
        paginaAtual = 1;
        await filtrarLembretes();

    } catch (error) {

        console.error(error);

        mostrarToast(`Não foi possível criar o lembrete. ${error.message}`, "erro");
    }
}



async function buscarLembrete(id) {

    try {

        const response = await requisitar(`/lembretes/${id}`);

        const lembrete = await response.json();

        limparErroDescricao();

        lembreteId.value = lembrete.id;
        descricao.value = lembrete.descricao;
        atualizarContador();

        tituloFormulario.textContent = "Editar lembrete";

        formulario.classList.remove("hidden");
        formulario.scrollIntoView({ behavior: "smooth", block: "start" });
        descricao.focus();

    } catch (error) {

        console.error(error);

        mostrarToast(`Não foi possível buscar o lembrete. ${error.message}`, "erro");
    }
}


async function atualizarLembrete() {

    try {

        const dados = {
            descricao: descricao.value.trim()
        };

        await requisitar(`/lembretes/${lembreteId.value}`, {
            method: "PUT",

            headers: {
                "Content-Type": "application/json"
            },

            body: JSON.stringify(dados)
        });

        mostrarToast("Lembrete atualizado com sucesso!", "sucesso");

        fecharFormulario();

        await filtrarLembretes();

    } catch (error) {

        console.error(error);

        mostrarToast(`Não foi possível atualizar o lembrete. ${error.message}`, "erro");
    }
}


async function excluirLembrete(id, texto) {

    const confirmou = await confirmar(`"${texto}" será removido da sua lista.`);

    if (!confirmou) {
        return;
    }

    try {

        await requisitar(`/lembretes/${id}`, {
            method: "DELETE"
        });

        mostrarToast("Lembrete excluído com sucesso!", "sucesso");

       
        if (lembreteId.value === id) {
            fecharFormulario();
        }

        await filtrarLembretes();

    } catch (error) {

        console.error(error);

        mostrarToast(`Não foi possível excluir o lembrete. ${error.message}`, "erro");
    }
}


function renderizarLembretes(lembretes) {
    listaLembretes.innerHTML = "";

    if (!lembretes || lembretes.length === 0) {
        mostrarEstado(mensagemVazia);

        return;
    }

    mostrarEstado(null);

    lembretes.forEach(lembrete => {
        const tr = document.createElement("tr");

        tr.innerHTML = `
            <td class="descricao">${escaparHtml(lembrete.descricao)}</td>

            <td class="data">${formatarData(lembrete.dataCadastro)}</td>

            <td class="data">${formatarData(lembrete.dataAtualizacao)}</td>

            <td>
                <span class="status ${lembrete.isAtivo ? "status-ativo" : "status-inativo"}">
                    ${lembrete.isAtivo ? "Ativo" : "Inativo"}
                </span>
            </td>

            <td>
                <div class="actions">
                    <button type="button" class="btn btn-edit" data-acao="editar">Editar</button>
                    <button type="button" class="btn btn-danger" data-acao="excluir">Excluir</button>
                </div>
            </td>
        `;

        tr.querySelector('[data-acao="editar"]')
            .addEventListener("click", () => buscarLembrete(lembrete.id));

        tr.querySelector('[data-acao="excluir"]')
            .addEventListener("click", () => excluirLembrete(lembrete.id, lembrete.descricao));

        listaLembretes.appendChild(tr);
    });
}

function renderizarPaginacao(total) {
    totalRegistros.textContent =
        `${total} ${total === 1 ? "registro" : "registros"}`;

    paginaAtualElemento.textContent =
        `Página ${paginaAtual} de ${totalPaginas}`;

    btnPrimeira.disabled = paginaAtual <= 1;
    btnAnterior.disabled = paginaAtual <= 1;
    btnProximo.disabled = paginaAtual >= totalPaginas;
    btnUltima.disabled = paginaAtual >= totalPaginas;

    paginas.innerHTML = "";

    calcularPaginasVisiveis().forEach(numero => {
        if (numero === "...") {
            const reticencias = document.createElement("span");
            reticencias.className = "page-ellipsis";
            reticencias.textContent = "…";
            paginas.appendChild(reticencias);
            return;
        }

        const botao = document.createElement("button");
        botao.type = "button";
        botao.className = "page-number";
        botao.textContent = numero;

        if (numero === paginaAtual) {
            botao.classList.add("active");
            botao.setAttribute("aria-current", "page");
        }

        botao.addEventListener("click", () => irParaPagina(numero));

        paginas.appendChild(botao);
    });
}

function calcularPaginasVisiveis() {
    const visiveis = [];

    for (let i = 1; i <= totalPaginas; i++) {
        if (i === 1 || i === totalPaginas || Math.abs(i - paginaAtual) <= 2) {
            visiveis.push(i);
        } else if (visiveis[visiveis.length - 1] !== "...") {
            visiveis.push("...");
        }
    }

    return visiveis;
}


function mostrarEstado(elemento) {
    [carregando, mensagemVazia, mensagemErro].forEach(estado => {
        estado.classList.toggle("hidden", estado !== elemento);
    });

    if (elemento === carregando) {
        listaLembretes.innerHTML = "";
    }
}

btnImprimir.addEventListener("click", () => {
    imprimirLembretes();
});

btnNovo.addEventListener("click", () => {
    abrirFormularioNovo();
});

btnCancelar.addEventListener("click", () => {
    fecharFormulario();
});

descricao.addEventListener("input", () => {
    atualizarContador();

    if (descricao.value.trim()) {
        limparErroDescricao();
    }
});

formLembrete.addEventListener("submit", async (event) => {
    event.preventDefault();

    if (!descricao.value.trim()) {
        descricao.classList.add("invalid");
        erroDescricao.classList.remove("hidden");
        descricao.focus();

        mostrarToast("Preencha a descrição do lembrete.", "aviso");

        return;
    }

     
    btnSalvar.disabled = true;

    try {
        if (lembreteId.value) {
            await atualizarLembrete();
        } else {
            await criarLembrete();
        }
    } finally {
        btnSalvar.disabled = false;
    }
});

function abrirFormularioNovo() {
    lembreteId.value = "";
    descricao.value = "";
    limparErroDescricao();
    atualizarContador();

    tituloFormulario.textContent = "Novo lembrete";

    formulario.classList.remove("hidden");
    descricao.focus();
}

function fecharFormulario() {
    formulario.classList.add("hidden");
    formLembrete.reset();
    lembreteId.value = "";
    limparErroDescricao();
}

function limparErroDescricao() {
    descricao.classList.remove("invalid");
    erroDescricao.classList.add("hidden");
}

function atualizarContador() {
    contadorDescricao.textContent = `${descricao.value.length}/500`;
}

formFiltro.addEventListener("submit", event => {
    event.preventDefault();

    if (!validarFiltros()) {
        return;
    }

    paginaAtual = 1;
    filtrarLembretes();
});

btnLimpar.addEventListener("click", () => {
    formFiltro.reset();
    limparErrosFiltro();

    paginaAtual = 1;
    filtrarLembretes();
});

tamanhoPagina.addEventListener("change", () => {
    paginaAtual = 1;
    filtrarLembretes();
});

[filtroCadastroInicial, filtroCadastroFinal, filtroAtualizacaoInicial, filtroAtualizacaoFinal]
    .forEach(campo => campo.addEventListener("change", limparErrosFiltro));

function validarFiltros() {
    limparErrosFiltro();

    const intervalos = [
        [filtroCadastroInicial, filtroCadastroFinal, "cadastro"],
        [filtroAtualizacaoInicial, filtroAtualizacaoFinal, "atualização"]
    ];

    for (const [inicial, final, nome] of intervalos) {
        
        if (inicial.value && final.value && inicial.value > final.value) {
            inicial.classList.add("invalid");
            final.classList.add("invalid");

            mostrarToast(`A data de ${nome} inicial não pode ser maior que a final.`, "aviso");

            return false;
        }
    }

    return true;
}

function limparErrosFiltro() {
    formFiltro.querySelectorAll(".invalid")
        .forEach(campo => campo.classList.remove("invalid"));
}

cabecalhosOrdenaveis.forEach(th => {
    th.querySelector("button").addEventListener("click", () => {
        const campo = th.dataset.ordem;

        // mesma coluna inverte a direção; coluna nova começa crescente
        if (campo === ordemAtual) {
            direcaoAtual = direcaoAtual === "ASC" ? "DESC" : "ASC";
        } else {
            ordemAtual = campo;
            direcaoAtual = "ASC";
        }

        atualizarIndicadoresOrdenacao();

        paginaAtual = 1;
        filtrarLembretes();
    });
});

function atualizarIndicadoresOrdenacao() {
    cabecalhosOrdenaveis.forEach(th => {
        const ativo = th.dataset.ordem === ordemAtual;

        th.setAttribute(
            "aria-sort",
            !ativo ? "none" : direcaoAtual === "ASC" ? "ascending" : "descending"
        );
    });
}

function irParaPagina(numero) {
    if (numero < 1 || numero > totalPaginas || numero === paginaAtual) {
        return;
    }

    paginaAtual = numero;
    filtrarLembretes();
}

btnPrimeira.addEventListener("click", () => irParaPagina(1));
btnAnterior.addEventListener("click", () => irParaPagina(paginaAtual - 1));
btnProximo.addEventListener("click", () => irParaPagina(paginaAtual + 1));
btnUltima.addEventListener("click", () => irParaPagina(totalPaginas));

btnTentarNovamente.addEventListener("click", () => filtrarLembretes());

function confirmar(mensagem) {
    textoModal.textContent = mensagem;
    modalConfirmacao.classList.remove("hidden");
    btnModalCancelar.focus();

    return new Promise(resolve => {
        const fechar = resultado => {
            modalConfirmacao.classList.add("hidden");

            btnModalConfirmar.removeEventListener("click", aoConfirmar);
            btnModalCancelar.removeEventListener("click", aoCancelar);
            modalConfirmacao.removeEventListener("click", aoClicarFora);
            document.removeEventListener("keydown", aoTeclar);

            resolve(resultado);
        };

        const aoConfirmar = () => fechar(true);
        const aoCancelar = () => fechar(false);
        const aoClicarFora = event => { if (event.target === modalConfirmacao) fechar(false); };
        const aoTeclar = event => { if (event.key === "Escape") fechar(false); };

        btnModalConfirmar.addEventListener("click", aoConfirmar);
        btnModalCancelar.addEventListener("click", aoCancelar);
        modalConfirmacao.addEventListener("click", aoClicarFora);
        document.addEventListener("keydown", aoTeclar);
    });
}

function formatarData(data) {

    if (!data) {
        return "-";
    }

    return new Date(data).toLocaleString("pt-BR");
}

function escaparHtml(texto) {
    const div = document.createElement("div");
    div.textContent = texto ?? "";
    return div.innerHTML;
}

const ICONES_TOAST = {
    sucesso: "✔",
    erro: "✖",
    aviso: "!"
};

function mostrarToast(mensagem, tipo = "aviso") {
    const container = document.getElementById("toasts");

    const toast = document.createElement("div");
    toast.className = `toast toast-${tipo}`;
    toast.setAttribute("role", tipo === "erro" ? "alert" : "status");

    const icone = document.createElement("span");
    icone.className = "toast-icon";
    icone.textContent = ICONES_TOAST[tipo] ?? "•";

    const texto = document.createElement("span");
    texto.textContent = mensagem;

    toast.append(icone, texto);
    container.appendChild(toast);

    setTimeout(() => {
        toast.classList.add("saindo");
        setTimeout(() => toast.remove(), 300);
    }, tipo === "erro" ? 5000 : 3000);
}
