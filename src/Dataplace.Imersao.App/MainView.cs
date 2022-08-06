using Dataplace.Core.Infra.CrossCutting.EventAggregator.Contracts;
using Dataplace.Core.win.Views;
using System;
using System.Windows.Forms;
using dpLibrary05;
using dpLibrary05.Infrastructure.Helpers.Permission;
using static dpLibrary05.mGenerico;
using Dataplace.Imersao.Presentation.Views.Orcamentos.Messages;
using Dataplace.Imersao.Presentation.Views.Orcamentos.Reports;
using Dataplace.Imersao.Presentation.Views.Orcamentos.Tools;
using Dataplace.Imersao.Presentation.Views.Providers;

namespace Dataplace.Imersao.App
{
    public partial class MainView : dpLibrary05.fMNU_Principal, ISubscriber<OrcamentoSetaAzulClick>
    {

        private readonly IEventAggregator _eventAggregator;
        public MainView(IEventAggregator eventAggregator)
        {
            AddMenu(new ToolStripMenuItem("Cancelar,Fechar e Reabrir orçamentos", null, (object sender, EventArgs e) =>
            {
                Dataplace.Core.win.Views.Managers.ViewManager.ShowViewOnForm<CancelarFehacrOrcamentosView>();
            }), TipoMenuEnun.Ferramenta);


            AddMenu(new ToolStripMenuItem("Orcamento", null, (object sender, EventArgs e) =>
            {
                CallOrcamento();

            }), TipoMenuEnun.Arquivo);

            _eventAggregator = eventAggregator;
            _eventAggregator.SubscribeMe(this);
        }

        #region menus
        private void CallOrcamento()
        {
            if (!PermissionAccess(467))
                return;
            Dataplace.Core.win.Views.Managers.ViewManager.ShowViewOnForm<InterfaceView, OrcamentoViewProvider>();
        }
        #endregion

        #region message
        public void OnEventHandler(OrcamentoSetaAzulClick e)
        {
            CallOrcamento();
            _eventAggregator.PublishEvent(new OrcamentoSelecionadoMessage(e.NumOrcamento));
        }
        #endregion

        #region intenal 
        private static bool PermissionAccess(int securityId) => PermissionAccess(securityId, true);

        private static bool PermissionAccess(int securityId, bool showMessage)
        {
            var permission = PermissionControl.Factory().ValidatePermission(securityId, PermissionEnum.Access);
            if (!permission.IsAuthorized() && showMessage)
            {
                MessageForm.Info(permission.BuildMessage());
            }
            return permission.IsAuthorized();
        }
        #endregion
    }
}
