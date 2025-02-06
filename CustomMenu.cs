using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MusicBeePlugin
{
    public class CustomMenu : ContextMenuStrip
    {
        public static Dictionary<string, CustomMenu> menus = new Dictionary<string, CustomMenu>();

        public static void AddMenu(string name, CustomMenu menu)
        {
            menus.Add(name, menu);
        }

        public static void ShowMenu(string name, Point location)
        {
            if (!menus.ContainsKey(name))
                throw new Exception($"Undefined custom menu `{name}`");
            menus[name].ShowMenu(location);
        }

        public void ShowMenu(Point location)
        {
            var control = Control.FromHandle(Plugin.mbApi.MB_GetWindowHandle());
            if (location == Cursor.Position)
                location = control.PointToClient(location);
            this.Show(control, location);
        }

        public void AddEntry(string path, Action action)
        {
            if (path == "[separator]")
            {
                this.Items.Add(new ToolStripSeparator());
                return;
            }

            var parts = path.Split('/');

            var currentCollection = this.Items;
            ToolStripMenuItem finalItem = null;

            foreach (var part in parts)
            {
                var existingItem = currentCollection.OfType<ToolStripMenuItem>()
                    .FirstOrDefault(item => item.Text == part);

                if (existingItem == null)
                {
                    var newItem = new ToolStripMenuItem(part);
                    currentCollection.Add(newItem);
                    finalItem = newItem;
                    currentCollection = newItem.DropDownItems;
                }
                else
                {
                    finalItem = existingItem;
                    currentCollection = existingItem.DropDownItems;
                }
            }

            if (finalItem != null && action != null)
            {
                finalItem.Click += (sender, e) => action.Invoke();
            }
        }
    }
}
