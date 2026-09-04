using System;
using System.ComponentModel;
using System.Drawing.Design;
using System.Linq;
using Re4QuadExtremeEditor.src.Forms;
using Re4QuadExtremeEditor.src.Class.MyProperty.CustomCollection;

namespace Re4QuadExtremeEditor.src.Class.MyProperty.CustomUITypeEditor
{
    public class EnemySearchForSnakeEditor : UITypeEditor
    {
        public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context)
        {
            return UITypeEditorEditStyle.Modal;
        }

        public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
        {
            if (context == null || context.Instance == null || provider == null)
            {
                return value;
            }

            UshortObjForListBox current = value as UshortObjForListBox;
            UshortObjForListBox result = current;

            SearchForm search = new SearchForm(
                ListBoxProperty.EnemiesList.Values.ToArray(),
                current);

            search.Search += (obj) =>
            {
                if (obj is UshortObjForListBox selected)
                {
                    result = selected;
                }
            };

            search.ShowDialog();

            return result;
        }
    }
}
