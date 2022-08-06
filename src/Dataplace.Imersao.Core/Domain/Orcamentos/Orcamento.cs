using Dataplace.Core.Domain.Entities;
using Dataplace.Core.Domain.Localization.Messages.Extensions;
using Dataplace.Imersao.Core.Domain.Exections;
using Dataplace.Imersao.Core.Domain.Orcamentos.Enums;
using Dataplace.Imersao.Core.Domain.Orcamentos.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Dataplace.Imersao.Core.Domain.Orcamentos
{
    public class Orcamento : Entity<Orcamento>
    {

        #region constructors

 
        // uso orm
        protected Orcamento() { Itens = new List<OrcamentoItem>(); }

        private Orcamento(string cdEmpresa, string cdFilial, OrcamentoCliente cliente,
            OrcamentoUsuario usuario, OrcamentoVendedor vendedor, OrcamentoTabelaPreco tabelaPreco)
        {
       
            CdEmpresa = cdEmpresa;
            CdFilial = cdFilial;
            Cliente = cliente;
            Usuario = usuario;
            Vendedor = vendedor;
            TabelaPreco = tabelaPreco;
          

            // default
            Situacao = OrcamentoStatusEnum.Aberto;
            DtOrcamento = DateTime.Now;
            VlTotal = 0;

            Itens = new List<OrcamentoItem>();

        }
        #endregion

        #region properties

        public string CdEmpresa { get; private set; }
        public string CdFilial { get; private set; }
        public int NumOrcamento { get; private set; }

        public DateTime DtOrcamento { get; private set; }
        public decimal VlTotal { get; private set; }
        public DateTime? DtFechamento { get; private set; }
        public OrcamentoStatusEnum Situacao { get; private set; }

        public OrcamentoCliente Cliente { get; private set; }
        public OrcamentoValidadeBase Validade { get; private set; }
        public OrcamentoTabelaPreco TabelaPreco { get; private set; }
        public OrcamentoVendedor Vendedor { get; private set; }
        public OrcamentoUsuario Usuario { get; private set; }

        public ICollection<OrcamentoItem> Itens { get; private set; }
        #endregion

        #region alteração dos itens

        public OrcamentoItem ObterItem(int seq)
        {
            return this.Itens.FirstOrDefault(x => x.Seq == seq);
        }
        public OrcamentoItem AdicionarItem(OrcamentoProduto produto, decimal quantidade, OrcamentoItemPreco preco)
        {

            var item = new OrcamentoItem(this.CdEmpresa, this.CdFilial, this.NumOrcamento, produto, quantidade, preco);
            if (!produto.IsValid())
                return default;

            this.Itens.Add(item);
            CalcularTotal();
            return item;

        }


        public OrcamentoItem RemoverItem(int seq)
        {
            var item = ObterItem(seq);

            if (item == null)
                return default;

            this.Itens.Remove(item);
            CalcularTotal();
            return item;

        }




        public void CalcularTotal()
        {
            this.VlTotal = this.Itens.Sum(x => x.Total);
        }
 
        #endregion

        #region alteração de status

        public bool FecharOrcamento()
        {
            Validation = new Dataplace.Core.Domain.DomainValidation.FluentValidator.Validation.ValidationContract();
            Validation.Requires()
                .IsTrue(this.Situacao == OrcamentoStatusEnum.Aberto, nameof(Situacao), "Somente orçamentos abertos podem ser fechados");
            if (!Validation.Valid)
                return false;

            Situacao = OrcamentoStatusEnum.Fechado;
            foreach (var item in Itens ?? new List<OrcamentoItem>())
            {
                item.DefinirStiaucao(OrcamentoItemStatusEnum.Fechado);
            }
            DtFechamento = DateTime.Now.Date;

            return true;
        }

        public bool ReabrirOrcamento()
        {

            Validation = new Dataplace.Core.Domain.DomainValidation.FluentValidator.Validation.ValidationContract();
            Validation.Requires()
                .IsTrue(this.Situacao == OrcamentoStatusEnum.Fechado, nameof(Situacao), "Somente orçamentos fechados podem ser reabertos");
            if (!Validation.Valid)
                return false;

    
            Situacao = OrcamentoStatusEnum.Aberto;
            foreach (var item in Itens ?? new List<OrcamentoItem>())
            {
                item.DefinirStiaucao(OrcamentoItemStatusEnum.Aberto);
            }
            DtFechamento = null;

            return true;
        }

        public bool CancelarOrcamento()
        {
            Validation = new Dataplace.Core.Domain.DomainValidation.FluentValidator.Validation.ValidationContract();
            Validation.Requires()
                .IsTrue(this.Situacao == OrcamentoStatusEnum.Aberto, nameof(Situacao), "Somente orçamentos abertos podem ser cancelados");
            if (!Validation.Valid)
                return false;

            Situacao = OrcamentoStatusEnum.Cancelado;
            foreach (var item in Itens ?? new List<OrcamentoItem>())
            {
                item.DefinirStiaucao(OrcamentoItemStatusEnum.Cancelado);
            }

            return true;
        }

        #endregion

        #region settets
        public bool DefinirPrevisaoEntrega(DateTime dtPrevisao)
        {
            Validation = new Dataplace.Core.Domain.DomainValidation.FluentValidator.Validation.ValidationContract();
            Validation.Requires()
                .IsTrue(this.Situacao == OrcamentoStatusEnum.Aberto, nameof(Situacao), "Somente orçamentos abertos podem ter sua data de previsão de entrega alterada")
                .IsTrue(this.DtOrcamento.Date < dtPrevisao, nameof(DtOrcamento), "A data de previsão de entrega precisa ser superior a data do orçamento");
            if (!Validation.Valid)
                return false;
            foreach (var item in Itens ?? new List<OrcamentoItem>())
            {
                item.DefinirPrevisaoEntrega(new OrcamentoPrevisaoEntrega(this, dtPrevisao));
            }
            return true;
        }

        internal bool RemoverPrevisaoEntrega()
        {
            Validation = new Dataplace.Core.Domain.DomainValidation.FluentValidator.Validation.ValidationContract();
            Validation.Requires()
                .IsTrue(this.Situacao == OrcamentoStatusEnum.Aberto, nameof(Situacao), "Somente orçamentos abertos podem ter sua data de previsão de entrega alterada");
            if (!Validation.Valid)
                return false;
            foreach (var item in Itens ?? new List<OrcamentoItem>())
            {
                item.RemoverPrevisaoEntrega();
            }
            return true;
        }

        public bool DefinirValidade(int validade)
        {
            Validation = new Dataplace.Core.Domain.DomainValidation.FluentValidator.Validation.ValidationContract();
            Validation.Requires()
                .IsTrue(this.Situacao == OrcamentoStatusEnum.Aberto, nameof(Situacao), "Somente orçamentos abertos podem ter sua data de validade alterada");
                
            if (!Validation.Valid)
                return false;

            this.Validade = new OrcamentoValidade(this, validade);
            return true;
            
        }

        public bool DefinirValidade(DateTime data)
        {
            Validation = new Dataplace.Core.Domain.DomainValidation.FluentValidator.Validation.ValidationContract();
            Validation.Requires()
                .IsTrue(this.Situacao == OrcamentoStatusEnum.Aberto, nameof(Situacao), "Somente orçamentos abertos podem ter sua data de validade alterada");
            if (!Validation.Valid)
                return false;

            this.Validade = new OrcamentoValidadePorData(this, data);
            return true;
        }

        public bool RemoverValidade()
        {
            Validation = new Dataplace.Core.Domain.DomainValidation.FluentValidator.Validation.ValidationContract();
            Validation.Requires()
                .IsTrue(this.Situacao == OrcamentoStatusEnum.Aberto, nameof(Situacao), "Somente orçamentos abertos podem ter sua data de validade alterada");
            if (!Validation.Valid)
                return false;

            this.Validade = null;
            return true;
        }

        public void DefinirUsuario(OrcamentoUsuario usuario)
        {
            this.Usuario = usuario.IsValid() ? usuario : default;
        }

        internal void DefinirVendedor(OrcamentoVendedor vendedor)
        {
            this.Vendedor = vendedor.IsValid() ? vendedor : default;
        }

        internal void RemoverVendedor()
        {
            this.Vendedor = default;
        }
   
        internal void DefinirValidade(OrcamentoValidade validade)
        {
            this.Validade = validade.IsValid() ? validade : default;
        }

        internal void DefinirCliente(OrcamentoCliente cliente)
        {
            this.Cliente = cliente.IsValid() ? cliente : default;
        }

        internal void RemoverCliente()
        {
            this.Cliente = null;
        }

        internal void DefinirTabelaPreco(OrcamentoTabelaPreco tabelaPreco)
        {
            this.TabelaPreco = tabelaPreco.IsValid() ? tabelaPreco : default;
        }

        internal void DefinirItens(IList<OrcamentoItem> itens)
        {
            this.Itens = itens;
        }

        #endregion

        #region validations

        internal bool PermiteAlteracaoItem()
        {
            Validation = new Dataplace.Core.Domain.DomainValidation.FluentValidator.Validation.ValidationContract();
            
            Validation.Requires()
                 .IsTrue(this.Situacao == OrcamentoStatusEnum.Aberto, nameof(Situacao), "O orçamento deve estar aberto para permitir inclusão, alteração ou exclusão de itens")
                 .IsNotNull(this.TabelaPreco, nameof(TabelaPreco), "O orçamento deve estar vinculado a uma tabela de preço para permitir inclusão, alteração ou exclusão de itens");

            return Validation.Valid;
        }

        internal bool PermiteAlteracao()
        {
            Validation = new Dataplace.Core.Domain.DomainValidation.FluentValidator.Validation.ValidationContract();

            Validation.Requires()
                 .IsTrue(this.Situacao == OrcamentoStatusEnum.Aberto, nameof(Situacao), "Somente orçamentos abertos podem ser alterados ou excluídos");

            return Validation.Valid;
        }

        public override bool IsValid()
        {
            Validation = new Dataplace.Core.Domain.DomainValidation.FluentValidator.Validation.ValidationContract();
            Validation.Requires()
                .IsNotNullOrEmpty(this.CdEmpresa, nameof(CdEmpresa), 18398.ToMessage())
                .IsNotNullOrEmpty(this.CdFilial, nameof(CdFilial), 13065.ToMessage())
                .IsNotNull(this.DtOrcamento, nameof(DtOrcamento), "A Data do orçamento é requirida!")
                .IsNotNull(this.Usuario, nameof(Usuario), "O Usuario é requirido!")
                .IsNotNull(this.TabelaPreco, nameof(TabelaPreco), "A Tabela de preço é requirido!");

            return Validation.Valid;
        }

     
        #endregion

        #region factory methods
        public static class Factory
        {
            public static Orcamento NovoOrcamento(
                string cdEmpresa, 
                string cdFilial, 
                OrcamentoCliente cliente, 
                OrcamentoUsuario usuario, 
                OrcamentoVendedor vendedor,
                OrcamentoTabelaPreco tabelaPreco)
            {
                return new Orcamento(cdEmpresa, cdFilial, cliente, usuario, vendedor, tabelaPreco);
            }
        }

     

        #endregion

    }
}
