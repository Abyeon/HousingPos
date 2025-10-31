using Dalamud.Plugin;

namespace HousingPos.Gui
{
    public abstract class Window<T>(T plugin)
        where T : IDalamudPlugin
    {
        protected bool WindowVisible;
        public virtual bool Visible
        {
            get => WindowVisible;
            set => WindowVisible = value;
        }
        protected T Plugin { get; } = plugin;

        public void Draw()
        {
            if (Visible)
            {
                DrawUi();
            }
            DrawScreen();
        }

        protected abstract void DrawUi();
        protected abstract void DrawScreen();
    }
}