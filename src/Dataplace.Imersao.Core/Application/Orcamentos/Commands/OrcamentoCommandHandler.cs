using Dataplace.Core.Comunications;
using Dataplace.Core.Domain.CommandHandlers;
using Dataplace.Core.Domain.Interfaces;
using Dataplace.Core.Domain.Interfaces.UoW;
using Dataplace.Core.Domain.Notifications;
using Dataplace.Imersao.Core.Application.Orcamentos.Events;
using Dataplace.Imersao.Core.Domain.Orcamentos;
using Dataplace.Imersao.Core.Domain.Orcamentos.Enums;
using Dataplace.Imersao.Core.Domain.Orcamentos.Repositories;
using Dataplace.Imersao.Core.Domain.Orcamentos.ValueObjects;
using Dataplace.Imersao.Core.Domain.Services;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Dataplace.Imersao.Core.Application.Orcamentos.Commands
{
    public class OrcamentoCommandHandler : CommandHandler,
        IRequestHandler<AdicionarOrcamentoCommand, bool>,
        IRequestHandler<AtualizarOrcamentoCommand, bool>,
        IRequestHandler<ExcluirOrcamentoCommand, bool>,
        IRequestHandler<ExcluirListItemCommand, bool>,
        IRequestHandler<FecharOrcamentoCommand, bool>,
        IRequestHandler<ReabrirOrcamentoCommand, bool>,
        IRequestHandler<CancelarOrcamentoCommand, bool>,
        IRequestHandler<AdicionarItemCommand, bool>,
        IRequestHandler<AtualizarItemCommand, bool>,
        IRequestHandler<ExcluirItemCommand, bool>,
        IRequestHandler<AtribuirDataDePrevisaoEntregaAoOrcamentoCommand, bool>,
        IRequestHandler<RemoverDataDePrevisaoEntregaAoOrcamentoCommand, bool>
        ,
        IRequestHandler<AdiconarValidadeOrcamentoCommand, bool>


    {

        #region fields
        private readonly IOrcamentoRepository _orcamentoRepository;
        private readonly IOrcamentoService _orcamentoService;
        private readonly IEnvironmentService _environmentService;
        #endregion

        #region constructor
        public OrcamentoCommandHandler(
            IUnitOfWork uow,
            IMediatorHandler bus,
            INotificationHandler<DomainNotification> notifications,
            IOrcamentoRepository orcamentoRepository,
            IOrcamentoService orcamentoService,
            IEnvironmentService environmentService) : base(uow, bus, notifications)
        {
            _orcamentoRepository = orcamentoRepository;
            _orcamentoService = orcamentoService;
            _environmentService = environmentService;
        }

        #endregion

        #region methods orçamento
        public async Task<bool> Handle(AdicionarOrcamentoCommand request, CancellationToken cancellationToken)
        {

            var transactionId = BeginTransaction();

            var cliente = request.Item.CdCliente.Trim().Length > 0 ? new OrcamentoCliente(request.Item.CdCliente) : ObterClientePadrao();
            var usuario = ObterUsuarioLogado();
            var tabelaPreco = string.IsNullOrEmpty(request.Item.CdTabela) && request.Item.SqTabela.HasValue ? new OrcamentoTabelaPreco(request.Item.CdTabela, request.Item.SqTabela.Value) : ObterTabelaPrecoPadrao();
            var vendedor = request.Item.CdVendedor.Trim().Length > 0 ? new OrcamentoVendedor(request.Item.CdVendedor) : ObterVendedorPadrao();

            var orcamento = Orcamento.Factory.NovoOrcamento(
                request.Item.CdEmpresa,
                request.Item.CdFilial,
                cliente,
                usuario,
                vendedor,
                tabelaPreco);

            if (request.Item.DiasValidade.HasValue && request.Item.DiasValidade.Value > 0)
                orcamento.DefinirValidade(request.Item.DiasValidade.Value);

            if (!orcamento.IsValid())
            {
                orcamento.Validation.Notifications.ToList().ForEach(val => NotifyErrorValidation(val.Property, val.Message));
                return false;
            }

            var result = _orcamentoRepository.AdicionarOrcamento(orcamento);
            if (!result.HasValue)
                NotifyErrorValidation("database", "Ocoreu um problema com a persistência dos dados");
            request.Item.NumOrcamento = result.Value;


            AddEvent(new OrcamentoAdicionadoEvent(request.Item));
            return Commit(transactionId);
        }

        public async Task<bool> Handle(AtualizarOrcamentoCommand request, CancellationToken cancellationToken)
        {


            var transactionId = BeginTransaction();

            //var length = 5;
            //NotifyProgress(0, length);
            //for (int i = 0; i < length; i++)
            //{
            //    System.Threading.Thread.Sleep(1000);
            //    NotifyProgress();
            //}


            var orcamento = _orcamentoRepository.ObterOrcamentoComItems(request.Item.CdEmpresa, request.Item.CdFilial, request.Item.NumOrcamento);
            if(orcamento == null)
            {
                NotifyErrorValidation("notFound", "Orçamento não encontrado");
                return false;
            }
            if (!orcamento.PermiteAlteracao())
            {
                orcamento.Validation.Notifications.ToList().ForEach(val => NotifyErrorValidation(val.Property, val.Message));
                return false;
            }


            // cliente
            if (request.Item.CdCliente.Trim().Length > 0)
                orcamento.DefinirCliente(new OrcamentoCliente(request.Item.CdCliente.Trim()));
            else
                orcamento.RemoverCliente();

            // validade
            if (request.Item.DiasValidade.HasValue  && request.Item.DiasValidade.Value > 0)
                 orcamento.DefinirValidade(request.Item.DiasValidade.Value);
            else
                orcamento.RemoverValidade();

            // vendedor
            if (request.Item.CdVendedor.Trim().Length > 0)
                orcamento.DefinirVendedor(new OrcamentoVendedor(request.Item.CdVendedor.Trim()));
            else
                orcamento.RemoverVendedor();


            // validar
            if (!orcamento.IsValid())
            {
                orcamento.Validation.Notifications.ToList().ForEach(val => NotifyErrorValidation(val.Property, val.Message));
                return false;
            }

            if (!_orcamentoRepository.AtualizarOrcamento(orcamento))
                NotifyErrorValidation("database", "Ocoreu um problema com a persistência dos dados");

           AddEvent(new OrcamentoAtualizadoEvent(request.Item));
           return Commit(transactionId);
        }

        public async Task<bool> Handle(ExcluirOrcamentoCommand request, CancellationToken cancellationToken)
        {
            var transactionId = BeginTransaction();

            var orcamento = _orcamentoRepository.ObterOrcamentoComItems(request.Item.CdEmpresa, request.Item.CdFilial, request.Item.NumOrcamento);
            if (orcamento == null)
            {
                NotifyErrorValidation("notFound", "Orçamento não encontrado");
                return false;
            }
            if (!orcamento.PermiteAlteracao())
            {
                orcamento.Validation.Notifications.ToList().ForEach(val => NotifyErrorValidation(val.Property, val.Message));
                return false;
            }

            //var items = _orcamentoRepository.ObterItems(orcamento.CdEmpresa, orcamento.CdFilial, orcamento.NumOrcamento);
            foreach (var item in orcamento.Itens)
            {
                if (!_orcamentoRepository.ExcluirItem(item))
                {
                    NotifyErrorValidation("database", "Ocoreu um problema com a persistência dos dados");
                    return false;
                }               
            }

            // validar
            if (!_orcamentoRepository.ExcluirOrcamento(orcamento))
                NotifyErrorValidation("database", "Ocoreu um problema com a persistência dos dados");

            AddEvent(new OrcamentoExcluidoEvent(request.Item));
            return Commit(transactionId);
        }

        public async Task<bool> Handle(FecharOrcamentoCommand request, CancellationToken cancellationToken)
        {

            var transactionId = BeginTransaction();

            var orcamento = _orcamentoRepository.ObterOrcamentoComItems(request.Item.CdEmpresa, request.Item.CdFilial, request.Item.NumOrcamento);
            if (orcamento == null)
            {
                NotifyErrorValidation("notFound", "Orçamento não encontrado");
                return false;
            }

            if (!orcamento.FecharOrcamento())
            {
                orcamento.Validation.Notifications.ToList().ForEach(val => NotifyErrorValidation(val.Property, val.Message));
                return false;
            }

            orcamento.CalcularTotal();
            if (!_orcamentoRepository.AtualizarOrcamento(orcamento))
                NotifyErrorValidation("database", "Ocoreu um problema com a persistência dos dados");
            foreach (var item in orcamento.Itens ?? new List<OrcamentoItem>())
            {
                if (!_orcamentoRepository.AtualizarItem(item))
                {
                    NotifyErrorValidation("database", "Ocoreu um problema com a persistência dos dados");
                    break;
                }
            }


            AddEvent(new OrcamentoFechadoEvent(request.Item));
            return Commit(transactionId);
        }

        public async Task<bool> Handle(ReabrirOrcamentoCommand request, CancellationToken cancellationToken)
        {
            var transactionId = BeginTransaction();

            var orcamento = _orcamentoRepository.ObterOrcamentoComItems(request.Item.CdEmpresa, request.Item.CdFilial, request.Item.NumOrcamento);
            if (orcamento == null)
            {
                NotifyErrorValidation("notFound", "Orçamento não encontrado");
                return false;
            }

            if (!orcamento.ReabrirOrcamento())
            {
                orcamento.Validation.Notifications.ToList().ForEach(val => NotifyErrorValidation(val.Property, val.Message));
                return false;
            }

            orcamento.CalcularTotal();
            if (!_orcamentoRepository.AtualizarOrcamento(orcamento))
                NotifyErrorValidation("database", "Ocoreu um problema com a persistência dos dados");
            foreach (var item in orcamento.Itens ?? new List<OrcamentoItem>())
            {
                if (!_orcamentoRepository.AtualizarItem(item))
                {
                    NotifyErrorValidation("database", "Ocoreu um problema com a persistência dos dados");
                    break;
                }
            }

            AddEvent(new OrcamentoReabertoEvent(request.Item));
            return Commit(transactionId);
        }

        public async Task<bool> Handle(CancelarOrcamentoCommand request, CancellationToken cancellationToken)
        {
            var transactionId = BeginTransaction();
            var orcamento = _orcamentoRepository.ObterOrcamentoComItems(request.Item.CdEmpresa, request.Item.CdFilial, request.Item.NumOrcamento);
            if (orcamento == null)
            {
                NotifyErrorValidation("notFound", "Orçamento não encontrado");
                return false;
            }


            orcamento.DefinirUsuario(new OrcamentoUsuario(_environmentService.GetUserName()));

            if (!orcamento.CancelarOrcamento())
            {
                orcamento.Validation.Notifications.ToList().ForEach(val => NotifyErrorValidation(val.Property, val.Message));
                return false;
            }
            orcamento.CalcularTotal();
            if (!_orcamentoRepository.AtualizarOrcamento(orcamento))
                NotifyErrorValidation("database", "Ocoreu um problema com a persistência dos dados");
            foreach (var item in orcamento.Itens ?? new List<OrcamentoItem> ())
            {
                if (!_orcamentoRepository.AtualizarItem(item))
                {
                    NotifyErrorValidation("database", "Ocoreu um problema com a persistência dos dados");
                    break;
                }        
            }

            AddEvent(new OrcamentoCanceladoEvent(request.Item));
            return Commit(transactionId);
        }

        public async Task<bool> Handle(AtribuirDataDePrevisaoEntregaAoOrcamentoCommand request, CancellationToken cancellationToken)
        {
            var transactionId = BeginTransaction();
            var orcamento = _orcamentoRepository.ObterOrcamentoComItems(request.Item.CdEmpresa, request.Item.CdFilial, request.Item.NumOrcamento);
            if (orcamento == null)
            {
                NotifyErrorValidation("notFound", "Orçamento não encontrado");
                return false;
            }

            if (!orcamento.DefinirPrevisaoEntrega(request.DtPrevisaoEntrega))
            {
                orcamento.Validation.Notifications.ToList().ForEach(val => NotifyErrorValidation(val.Property, val.Message));
                return false;
            }
            orcamento.CalcularTotal();
            if (!_orcamentoRepository.AtualizarOrcamento(orcamento))
                NotifyErrorValidation("database", "Ocoreu um problema com a persistência dos dados");
            foreach (var item in orcamento.Itens ?? new List<OrcamentoItem>())
            {
                if (!_orcamentoRepository.AtualizarItem(item))
                {
                    NotifyErrorValidation("database", "Ocoreu um problema com a persistência dos dados");
                    break;
                }
            }

            AddEvent(new DataDePrevisaoDeEntregaAtribuidaAoOrcamentoEvent(request.Item, request.DtPrevisaoEntrega));
            return Commit(transactionId);
        }



        public async Task<bool> Handle(AdiconarValidadeOrcamentoCommand request, CancellationToken cancellationToken)
        {
            var transactionId = BeginTransaction();
            var orcamento = _orcamentoRepository.ObterOrcamentoComItems(request.Item.CdEmpresa, request.Item.CdFilial, request.Item.NumOrcamento);
            if (orcamento == null)
            {
                NotifyErrorValidation("notFound", "Orçamento não encontrado");
                return false;
            }

            if (!orcamento.DefinirPrevisaoEntrega(request.Dtvalidade))
            {
                orcamento.Validation.Notifications.ToList().ForEach(val => NotifyErrorValidation(val.Property, val.Message));
                return false;
            }
            orcamento.CalcularTotal();
            if (!_orcamentoRepository.AtualizarOrcamento(orcamento))
                NotifyErrorValidation("database", "Ocoreu um problema com a persistência dos dados");
            foreach (var item in orcamento.Itens ?? new List<OrcamentoItem>())
            {
                if (!_orcamentoRepository.AtualizarItem(item))
                {
                    NotifyErrorValidation("database", "Ocoreu um problema com a persistência dos dados");
                    break;
                }
            }

            AddEvent(new DataDePrevisaoDeEntregaAtribuidaAoOrcamentoEvent(request.Item, request.Dtvalidade));
            return Commit(transactionId);
        }


        public async Task<bool> Handle(RemoverDataDePrevisaoEntregaAoOrcamentoCommand request, CancellationToken cancellationToken)
        {
            var transactionId = BeginTransaction();
            var orcamento = _orcamentoRepository.ObterOrcamentoComItems(request.Item.CdEmpresa, request.Item.CdFilial, request.Item.NumOrcamento);
            if (orcamento == null)
            {
                NotifyErrorValidation("notFound", "Orçamento não encontrado");
                return false;
            }

            if (!orcamento.RemoverPrevisaoEntrega())
            {
                orcamento.Validation.Notifications.ToList().ForEach(val => NotifyErrorValidation(val.Property, val.Message));
                return false;
            }


            orcamento.CalcularTotal();
            if (!_orcamentoRepository.AtualizarOrcamento(orcamento))
                NotifyErrorValidation("database", "Ocoreu um problema com a persistência dos dados");
            foreach (var item in orcamento.Itens ?? new List<OrcamentoItem>())
            {
                if (!_orcamentoRepository.AtualizarItem(item))
                {
                    NotifyErrorValidation("database", "Ocoreu um problema com a persistência dos dados");
                    break;
                }
            }

            AddEvent(new DataDePrevisaoDeEntregaRemovidaAoOrcamentoEvent(request.Item));
            return Commit(transactionId);
        }
        #endregion

        #region methods orçamento item
        public async Task<bool> Handle(AdicionarItemCommand request, CancellationToken cancellationToken)
        {
            var transactionId = BeginTransaction();
            var orcamento = _orcamentoRepository.ObterOrcamentoComItems(request.Item.CdEmpresa, request.Item.CdFilial, request.Item.NumOrcamento);
            if (orcamento == null)
            {
                NotifyErrorValidation("notFound", "Orçamento não encontrado");
                return false;
            }

            if(!orcamento.PermiteAlteracaoItem())
            {
                orcamento.Validation.Notifications.ToList().ForEach(val => NotifyErrorValidation(val.Property, val.Message));
                return false;
            }

            var tpRegistro = request.Item.TpRegistro.ToTpRegistroEnum();
            var produto = !string.IsNullOrEmpty((request.Item.CdProduto ?? "").Trim()) && tpRegistro.HasValue ? 
                new OrcamentoProduto(tpRegistro.Value, request.Item.CdProduto)  :default;

            var quantidade = request.Item.Quantidade;

            if (produto == null)
            {
                NotifyErrorValidation("notFound", "Dados do produto inválido");
                return false;
            }
 
            var preco = _orcamentoService.ObterProdutoPreco(orcamento, produto);
            if (preco == null)
            {
                NotifyErrorValidation("notFound", "Dados do preço inválido");
                return false;
            }

            var item = orcamento.AdicionarItem(produto, quantidade, preco);
            var r = _orcamentoRepository.AdicionarItem(item);
            if (!r.HasValue)
                NotifyErrorValidation("database", "Ocoreu um problema com a persistência dos dados");
            request.Item.Seq = r.Value;

            if (!_orcamentoRepository.AtualizarOrcamento(orcamento))
                NotifyErrorValidation("database", "Ocoreu um problema com a persistência dos dados");

            AddEvent(new ItemAdicionadoEvent(request.Item));
            return  Commit(transactionId);
        }

        public async Task<bool> Handle(AtualizarItemCommand request, CancellationToken cancellationToken)
        {
            var transactionId = BeginTransaction();
            var orcamento = _orcamentoRepository.ObterOrcamentoComItems(request.Item.CdEmpresa, request.Item.CdFilial, request.Item.NumOrcamento);
            if (orcamento == null)
            {
                NotifyErrorValidation("notFound", "Orçamento não encontrado");
                return false;
            }
            if (!orcamento.PermiteAlteracaoItem())
            {
                orcamento.Validation.Notifications.ToList().ForEach(val => NotifyErrorValidation(val.Property, val.Message));
                return false;
            }

            //var item = _orcamentoRepository.ObterItem(orcamento.CdEmpresa, orcamento.CdFilial, orcamento.NumOrcamento, request.Item.Seq);
            var item = orcamento.ObterItem(request.Item.Seq);
            if (item == null)
            {
                NotifyErrorValidation("notFound", "Item do orçamento não encontrado");
                return false;
            }

            var tpRegistro = request.Item.TpRegistro.ToTpRegistroEnum();
            var produto = !string.IsNullOrEmpty((request.Item.CdProduto ?? "").Trim()) && tpRegistro.HasValue ?
                new OrcamentoProduto(tpRegistro.Value, request.Item.CdProduto) : default;

            var quantidade = request.Item.Quantidade;
            item.DefinirQuantidade(quantidade);

            if (produto == null)
            {
                NotifyErrorValidation("notFound", "Dados do produto inválido");
                return false;
            }
            item.DefinirProduto(produto);

            //var preco = new OrcamentoItemPreco();
            //if (preco == null)
            //{
            //    NotifyErrorValidation("notFound", "Dados do preço inválido");
            //    return false;
            //}
            //item.DefinirPreco(preco);

            if (!_orcamentoRepository.AtualizarItem(item))
                NotifyErrorValidation("database", "Ocoreu um problema com a persistência dos dados");

            orcamento.CalcularTotal();
            if (!_orcamentoRepository.AtualizarOrcamento(orcamento))
                NotifyErrorValidation("database", "Ocoreu um problema com a persistência dos dados");

            AddEvent(new ItemAtualizadoEvent(request.Item));
            return Commit(transactionId);
        }

        public async Task<bool> Handle(ExcluirItemCommand request, CancellationToken cancellationToken)
        {
            var transactionId = BeginTransaction();

            //var length = 5;
            //NotifyProgress(0, length);
            //for (int i = 0; i < length; i++)
            //{
            //    System.Threading.Thread.Sleep(1000);
            //    NotifyProgress();
            //}

            var orcamento = _orcamentoRepository.ObterOrcamentoComItems(request.Item.CdEmpresa, request.Item.CdFilial, request.Item.NumOrcamento);
            if (orcamento == null)
            {
                NotifyErrorValidation("notFound", "Orçamento não encontrado");
                return false;
            }
            if (!orcamento.PermiteAlteracaoItem())
            {
                orcamento.Validation.Notifications.ToList().ForEach(val => NotifyErrorValidation(val.Property, val.Message));
                return false;
            }

            //var item = _orcamentoRepository.ObterItem(request.Item.CdEmpresa, request.Item.CdFilial, request.Item.NumOrcamento, request.Item.Seq);
            var item = orcamento.RemoverItem(request.Item.Seq);
            if (item == null)
            {
                NotifyErrorValidation("notFound", "Item não encontrado");
                return false;
            }



            if (!_orcamentoRepository.ExcluirItem(item))
                NotifyErrorValidation("database", "Ocoreu um problema com a persistência dos dados");

            if (!_orcamentoRepository.AtualizarOrcamento(orcamento))
                NotifyErrorValidation("database", "Ocoreu um problema com a persistência dos dados");

            AddEvent(new ItemExcluidoEvent(request.Item));
            return Commit(transactionId);
        }

        public async Task<bool> Handle(ExcluirListItemCommand request, CancellationToken cancellationToken)
        {

            cancellationToken.ThrowIfCancellationRequested();

            var transactionId = BeginTransaction();

            if (request.Items == null || !request.Items.Any())
                return true;

            //var length = 5;
            //NotifyProgress(0, length);
            //for (int i = 0; i < length; i++)
            //{
            //    System.Threading.Thread.Sleep(1000);
            //    NotifyProgress();
            //}

            var first = request.Items.First();
            var orcamento = _orcamentoRepository.ObterOrcamentoComItems (first.CdEmpresa, first.CdFilial, first.NumOrcamento);
            if (orcamento == null)
            {
                NotifyErrorValidation("notFound", "Orçamento não encontrado");
                return false;
            }
            if (!orcamento.PermiteAlteracaoItem())
            {
                orcamento.Validation.Notifications.ToList().ForEach(val => NotifyErrorValidation(val.Property, val.Message));
                return false;
            }
    

            foreach (var i in request.Items)
            {
                //var item = _orcamentoRepository.ObterItem(i.CdEmpresa, i.CdFilial, i.NumOrcamento, i.Seq);
                var item = orcamento.RemoverItem( i.Seq);
                if (item == null)
                {
                    NotifyErrorValidation("notFound", "Item não encontrado");
                    return false;
                }

                if (!_orcamentoRepository.ExcluirItem(item))
                {
                    NotifyErrorValidation("database", "Ocoreu um problema com a persistência dos dados");
                    break;
                }
                AddEvent(new ItemExcluidoEvent(i));
            }

            if (!_orcamentoRepository.AtualizarOrcamento(orcamento))
                NotifyErrorValidation("database", "Ocoreu um problema com a persistência dos dados");

            return Commit(transactionId);
        }

  
        #endregion

        #region internals
        public OrcamentoCliente ObterClientePadrao()
        {
            return new OrcamentoCliente("31112");
        }
        public OrcamentoTabelaPreco ObterTabelaPrecoPadrao()
        {
            return new OrcamentoTabelaPreco("2005", 1);
        }

        public OrcamentoUsuario ObterUsuarioLogado()
        {
            return new OrcamentoUsuario(_environmentService.GetUserName());
        }
        public OrcamentoVendedor ObterVendedorPadrao()
        {
            return new OrcamentoVendedor("00");
        }

        private object ObterDadosDoProduto()
        {
            throw new NotImplementedException();
        }

        #endregion

    }
}
