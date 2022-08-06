using Dapper;
using Dataplace.Core.Domain.Query;
using Dataplace.Core.Infra.Data.SqlConnection;
using Dataplace.Imersao.Core.Application.Orcamentos.ViewModels;
using Dataplace.Imersao.Core.Domain.Orcamentos.Enums;
using dpLibrary05;
using MediatR;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Dataplace.Imersao.Core.Application.Orcamentos.Queries
{
    public class OrcamentoQueryHandler :
        IRequestHandler<OrcamentoQuery, IEnumerable<OrcamentoViewModel>>,
        IRequestHandler<OrcamentoRefreshQuery, OrcamentoViewModel>,
        IRequestHandler<OrcamentoMoveQuery, OrcamentoViewModel>,
        IRequestHandler<OrcamentoItemQuery, IEnumerable<OrcamentoItemViewModel>>,
        IRequestHandler<OrcamentoItemRefreshQuery, OrcamentoItemViewModel>
    {

        #region fields
        private readonly IDataAccess _dataAccess;
        #endregion

        #region contructors
        public OrcamentoQueryHandler(ISqlDataAccess dataAccess)
        {
            _dataAccess = dataAccess;
        }
        #endregion

        #region methods orcamento
        public async Task<IEnumerable<OrcamentoViewModel>> Handle(OrcamentoQuery request, CancellationToken cancellationToken)
        {
            var sql = $@"
            SET TRANSACTION ISOLATION LEVEL SNAPSHOT;

            SELECT 
	            Orcamento.CdEmpresa, 
		        Orcamento.CdFilial, 
		        Orcamento.NumOrcamento, 
                Orcamento.StOrcamento as Situacao,
		        Orcamento.CdCliente, 
                Empresa.razao AS DsCliente,
		        Orcamento.DtOrcamento,
		        Orcamento.vlvendar as VlTotal,
	            Orcamento.CdVendedor,
                Vendedor.Nome as NomeVendedor,
		        Orcamento.numdias as DiasValidade,
		        Orcamento.dtvalidade DataValidade,	     
		        Orcamento.DtFechamento,
		        Orcamento.Usuario,
		   		Orcamento.CdTabela,
		        Orcamento.SqTabela,
                -- previsão de entrega vem dos itens
                (SELECT COUNT(1) FROM OrcamentoItem where OrcamentoItem.numorcamento = Orcamento.numorcamento and OrcamentoItem.CdEmpresa = Orcamento.CdEmpresa AND OrcamentoItem.CdFilial = Orcamento.CdFilial) AS TotalItens,
                (SELECT MIN(OrcamentoItem.dtpreventrega) FROM OrcamentoItem where OrcamentoItem.numorcamento = Orcamento.numorcamento and OrcamentoItem.CdEmpresa = Orcamento.CdEmpresa AND OrcamentoItem.CdFilial = Orcamento.CdFilial) AS DtPrevEntrega

	        FROM Orcamento
                INNER JOIN cliente 
                    ON cliente.cdcliente = orcamento.cdcliente
                        AND cliente.cdempresa = orcamento.cdempresa
                        AND cliente.cdfilial = orcamento.cdfilial
                INNER JOIN Empresa 
                    ON Empresa.empresaid = cliente.empresaid
                        AND Empresa.cdempresa = cliente.cdempresa
                        AND Empresa.cdfilial = cliente.cdfilial
                INNER JOIN Vendedor 
                    ON Vendedor.cdVendedor = Orcamento.cdVendedor
                        AND Vendedor.cdempresa = Orcamento.cdempresa
                        AND Vendedor.cdfilial = Orcamento.cdfilial

            /**where**/	
            /**orderby**/
            ";
            var builder = new SqlBuilder();
            var selector = builder.AddTemplate(sql);


            if (request.Situacao.HasValue)
                builder.Where("orcamento.StOrcamento = @Situacao", new { Situacao = request.Situacao.Value.ToDataValue()});

            if (request.SituacaoList != null && request.SituacaoList.Count > 0)
                builder.Where($"orcamento.StOrcamento IN ('{string.Join("','", request.SituacaoList.Select(x => x.ToDataValue()))}')");

            if(request.DtInicio.HasValue && request.DtFim.HasValue)
                builder.Where("orcamento.DtOrcamento between @DtInicio AND @DtFim ", new { DtInicio = request.DtInicio.Value.Date, DtFim = request.DtFim.Value.Date.AddDays(1).AddSeconds(-1) });
            if (!string.IsNullOrEmpty(request.Cdvendedor))
                builder.Where($"orcamento.Cdvendador IN ('{request.Cdvendedor}')");


            builder.OrderBy("orcamento.DtOrcamento DESC");

            var cmd = new CommandDefinition(selector.RawSql, selector.Parameters, flags: CommandFlags.NoCache);

            return _dataAccess.Connection.Query<OrcamentoViewModel>(cmd);
        }

        public async Task<OrcamentoViewModel> Handle(OrcamentoRefreshQuery query, CancellationToken cancellationToken)
        {
            var sql = $@"
            SET TRANSACTION ISOLATION LEVEL SNAPSHOT;
            SELECT 
	            Orcamento.CdEmpresa, 
		        Orcamento.CdFilial, 
		        Orcamento.NumOrcamento, 
		        Orcamento.CdCliente, 
		        Orcamento.DtOrcamento,
		        Orcamento.vlvendar as VlTotal,
		        Orcamento.numdias as DiasValidade,
		        Orcamento.dtvalidade DataValidade,	
		        Orcamento.CdTabela,
		        Orcamento.SqTabela,
		        Orcamento.DtFechamento,
		        Orcamento.CdVendedor,
		        Orcamento.Usuario,
		        Orcamento.StOrcamento as Situacao,
                -- previsão de entrega vem dos itens
                (SELECT COUNT(1) FROM OrcamentoItem where OrcamentoItem.numorcamento = Orcamento.numorcamento and OrcamentoItem.CdEmpresa = Orcamento.CdEmpresa AND OrcamentoItem.CdFilial = Orcamento.CdFilial) AS TotalItens,
                (SELECT MIN(OrcamentoItem.dtpreventrega) FROM OrcamentoItem where OrcamentoItem.numorcamento = Orcamento.numorcamento and OrcamentoItem.CdEmpresa = Orcamento.CdEmpresa AND OrcamentoItem.CdFilial = Orcamento.CdFilial) AS DtPrevEntrega
	        FROM Orcamento
            /**where**/	
            ";
            var builder = new SqlBuilder();
            var selector = builder.AddTemplate(sql);

            
            builder.Where("orcamento.NumOrcamento = @NumOrcamento", new { query.NumOrcamento });

            var cmd = new CommandDefinition(selector.RawSql, selector.Parameters, flags: CommandFlags.NoCache);
            
            return _dataAccess.Connection.QueryFirstOrDefault<OrcamentoViewModel>(cmd);
        }

        public async Task<OrcamentoViewModel> Handle(OrcamentoMoveQuery query, CancellationToken cancellationToken)
        {

            var sql = $@"
                SET TRANSACTION ISOLATION LEVEL SNAPSHOT;
                SELECT 
	                Orcamento.CdEmpresa, 
		            Orcamento.CdFilial, 
		            Orcamento.NumOrcamento, 
		            Orcamento.CdCliente, 
		            Orcamento.DtOrcamento,
		            Orcamento.vlvendar as VlTotal,
		            Orcamento.numdias as DiasValidade,
		            Orcamento.dtvalidade DataValidade,	
		            Orcamento.CdTabela,
		            Orcamento.SqTabela,
		            Orcamento.DtFechamento,
		            Orcamento.CdVendedor,
		            Orcamento.Usuario,
		            Orcamento.StOrcamento as Situacao,
                    -- previsão de entrega vem dos itens
                    (SELECT COUNT(1) FROM OrcamentoItem where OrcamentoItem.numorcamento = Orcamento.numorcamento and OrcamentoItem.CdEmpresa = Orcamento.CdEmpresa AND OrcamentoItem.CdFilial = Orcamento.CdFilial) AS TotalItens,
                    (SELECT MIN(OrcamentoItem.dtpreventrega) FROM OrcamentoItem where OrcamentoItem.numorcamento = Orcamento.numorcamento and OrcamentoItem.CdEmpresa = Orcamento.CdEmpresa AND OrcamentoItem.CdFilial = Orcamento.CdFilial) AS DtPrevEntrega
	            FROM Orcamento
                /**where**/	
                /**orderby**/
            ";

            var builder = new SqlBuilder();
            var selector = builder.AddTemplate(sql);
            switch (query.MoveType)
            {
                case OnMoveTypeEnum.MoveFirst:
                    builder.OrderBy($"NumOrcamento ASC");
                    break;
                case OnMoveTypeEnum.MoveLast:
                    builder.OrderBy($"NumOrcamento DESC");
                    break;
                case OnMoveTypeEnum.MoveNext:
                    builder.Where($" (NumOrcamento > @NumOrcamento OR @NumOrcamento is null) ", new { NumOrcamento = query.CurrentItem?.NumOrcamento });
                    builder.OrderBy($"NumOrcamento ASC");
                    break;
                case OnMoveTypeEnum.MovePrevious:
                    builder.Where($" (NumOrcamento < @NumOrcamento OR @NumOrcamento is null) ", new { NumOrcamento = query.CurrentItem?.NumOrcamento });
                    builder.OrderBy($"NumOrcamento DESC");
                    break;

                default:
                    break;
            }
            var cmd = new CommandDefinition(selector.RawSql, selector.Parameters, flags: CommandFlags.NoCache);
            return   _dataAccess.Connection.QueryFirstOrDefault<OrcamentoViewModel>(cmd);
        }

        #endregion

        #region methods Orcamento Item
        public async Task<IEnumerable<OrcamentoItemViewModel>> Handle(OrcamentoItemQuery query, CancellationToken cancellationToken)
        {
            var sql = $@"
            SET TRANSACTION ISOLATION LEVEL SNAPSHOT;
            SELECT 
                OrcamentoItem.Seq, 
	            OrcamentoItem.CdEmpresa, 
		        OrcamentoItem.CdFilial, 
		        OrcamentoItem.NumOrcamento, 
		        OrcamentoItem.TpRegistro, 
		        OrcamentoItem.CdProduto,
                Produto.DsVenda as DsProduto,
				OrcamentoItem.qtdproduto  as Quantidade,
				OrcamentoItem.vlvenda as PrecoTabela,
                OrcamentoItem.percaltpreco as PercAltPreco,
				OrcamentoItem.vlcalculado as PrecoVenda,
		        OrcamentoItem.vlcalculado * OrcamentoItem.qtdproduto as Total,
		        OrcamentoItem.stitem as Status,
                OrcamentoItem.QtdDiaPrevEntrega, 
                OrcamentoItem.DtPrevEntrega
	        FROM OrcamentoItem
				INNER JOIN Produto 
					ON Produto.CdProduto = OrcamentoItem.CdProduto
						AND Produto.TpProduto = OrcamentoItem.tpregistro
						AND Produto.CdEmpresa = OrcamentoItem.CdEmpresa
						AND Produto.CdFilial = OrcamentoItem.CdFilial
            /**where**/	
            ";

            var builder = new SqlBuilder();
            var selector = builder.AddTemplate(sql);

            builder.Where("OrcamentoItem.NumOrcamento = @NumOrcamento", new { query.NumOrcamento });

            var cmd = new CommandDefinition(selector.RawSql, selector.Parameters, flags: CommandFlags.NoCache);

            return   _dataAccess.Connection.Query<OrcamentoItemViewModel>(cmd);
        }

        public async Task<OrcamentoItemViewModel> Handle(OrcamentoItemRefreshQuery query, CancellationToken cancellationToken)
        {
            var sql = $@"
            SET TRANSACTION ISOLATION LEVEL SNAPSHOT;
            SELECT 
                OrcamentoItem.Seq, 
	            OrcamentoItem.CdEmpresa, 
		        OrcamentoItem.CdFilial, 
		        OrcamentoItem.NumOrcamento, 
		        OrcamentoItem.TpRegistro, 
		        OrcamentoItem.CdProduto,
                Produto.DsVenda as DsProduto,
				OrcamentoItem.qtdproduto  as Quantidade,
				OrcamentoItem.vlvenda as PrecoTabela,
                OrcamentoItem.percaltpreco as PercAltPreco,
				OrcamentoItem.vlcalculado as PrecoVenda,
		        OrcamentoItem.vlcalculado * OrcamentoItem.qtdproduto as Total,
		        OrcamentoItem.stitem as Status,
                OrcamentoItem.QtdDiaPrevEntrega, 
                OrcamentoItem.DtPrevEntrega
	        FROM OrcamentoItem
				INNER JOIN Produto 
					ON Produto.CdProduto = OrcamentoItem.CdProduto
						AND Produto.TpProduto = OrcamentoItem.tpregistro
						AND Produto.CdEmpresa = OrcamentoItem.CdEmpresa
						AND Produto.CdFilial = OrcamentoItem.CdFilial
            /**where**/	
            ";

            var builder = new SqlBuilder();
            var selector = builder.AddTemplate(sql);

            builder.Where("OrcamentoItem.Seq = @Seq", new { query.Seq });

            var cmd = new CommandDefinition(selector.RawSql, selector.Parameters, flags: CommandFlags.NoCache);
            return  _dataAccess.Connection.QueryFirstOrDefault<OrcamentoItemViewModel>(cmd);
        }

    

        #endregion

    }
}
