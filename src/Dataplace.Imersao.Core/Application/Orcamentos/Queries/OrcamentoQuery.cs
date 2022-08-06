using Dataplace.Core.Domain.Query;
using Dataplace.Imersao.Core.Application.Orcamentos.ViewModels;
using Dataplace.Imersao.Core.Domain.Orcamentos.Enums;
using System;
using System.Collections.Generic;

namespace Dataplace.Imersao.Core.Application.Orcamentos.Queries
{
    public class OrcamentoQuery : QuerySort<IEnumerable<OrcamentoViewModel>>, IQuerySort<IEnumerable<OrcamentoViewModel>>
    {
        public OrcamentoStatusEnum? Situacao { get; set; }
        public IList<OrcamentoStatusEnum> SituacaoList { get; set; }
        public DateTime? DtInicio { get; set; }
        public DateTime? DtFim { get; set; }
        public string Cdvendedor { get; set; }

       

       
    }


}
