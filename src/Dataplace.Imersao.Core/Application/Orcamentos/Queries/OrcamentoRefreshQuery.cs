﻿using Dataplace.Core.Domain.Query;
using Dataplace.Imersao.Core.Application.Orcamentos.ViewModels;

namespace Dataplace.Imersao.Core.Application.Orcamentos.Queries
{
    public class OrcamentoRefreshQuery : QueryRefeshItem<OrcamentoViewModel>, IQueryRefeshItem<OrcamentoViewModel>
    {
        public int NumOrcamento { get; set; }
    }


}
