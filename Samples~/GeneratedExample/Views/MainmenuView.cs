namespace Game.Ui
{
    public partial class MainmenuView
    {
        private readonly AudioToggleWiring _audioToggles = new AudioToggleWiring();

        protected override void OnBind()
        {
            _audioToggles.Bind(this, "popup_setting", "image_panel_setting");
        }

        protected override void OnUnwire()
        {
            _audioToggles.Unwire();
        }
    }
}
