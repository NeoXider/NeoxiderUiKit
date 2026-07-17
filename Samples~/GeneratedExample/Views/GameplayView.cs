namespace Game.Ui
{
    public partial class GameplayView
    {
        private readonly AudioToggleWiring _audioToggles = new AudioToggleWiring();

        protected override void OnBind()
        {
            _audioToggles.Bind(this, "popup_pause", "image_panel_pause");
        }

        protected override void OnUnwire()
        {
            _audioToggles.Unwire();
        }
    }
}
