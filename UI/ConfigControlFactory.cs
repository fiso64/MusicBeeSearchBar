using MusicBeePlugin.Config;
using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace MusicBeePlugin.UI
{
    public static class ConfigControlFactory
    {
        public static void CreateControlsForObject(TableLayoutPanel layout, object configObject, string category)
        {
            var properties = configObject.GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(p => new { Property = p, Attr = p.GetCustomAttribute<ConfigPropertyAttribute>() })
                .Where(p => p.Attr != null && p.Attr.Category == category);

            layout.SuspendLayout();
            layout.Controls.Clear();
            layout.RowCount = 0;
            layout.RowStyles.Clear();
            
            var toolTip = new ToolTip();

            foreach (var propInfo in properties)
            {
                layout.RowCount++;
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

                if (propInfo.Property.PropertyType == typeof(bool))
                {
                    // Special case for bool: [CheckBox] Label Text
                    var checkbox = (CheckBox)CreateControlForProperty(propInfo.Property, configObject);
                    checkbox.Text = propInfo.Attr.DisplayName ?? propInfo.Property.Name;
                    checkbox.AutoSize = true;
                    checkbox.Anchor = AnchorStyles.Left;

                    if (!string.IsNullOrEmpty(propInfo.Attr.Description))
                    {
                        toolTip.SetToolTip(checkbox, propInfo.Attr.Description);
                    }

                    layout.Controls.Add(checkbox, 0, layout.RowCount - 1);
                    layout.SetColumnSpan(checkbox, 2);
                }
                else
                {
                    // Standard case: Label: [Control]
                    var label = new Label
                    {
                        Text = (propInfo.Attr.DisplayName ?? propInfo.Property.Name) + ":",
                        AutoSize = true,
                        Anchor = AnchorStyles.Left,
                        TextAlign = ContentAlignment.MiddleLeft,
                        Dock = DockStyle.Fill
                    };

                    if (!string.IsNullOrEmpty(propInfo.Attr.Description))
                    {
                        toolTip.SetToolTip(label, propInfo.Attr.Description);
                    }
                
                    layout.Controls.Add(label, 0, layout.RowCount - 1);

                    Control control = CreateControlForProperty(propInfo.Property, configObject);
                    if (control != null)
                    {
                        control.Anchor = AnchorStyles.Left;
                        if (!string.IsNullOrEmpty(propInfo.Attr.Description))
                        {
                            toolTip.SetToolTip(control, propInfo.Attr.Description);
                        }
                        layout.Controls.Add(control, 1, layout.RowCount - 1);
                    }
                }
            }
            
            // Add a spacer row that fills the remaining vertical space, pushing content rows to the top
            layout.RowCount++;
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            var spacer = new Control { Dock = DockStyle.Fill };
            layout.Controls.Add(spacer, 0, layout.RowCount - 1);
            layout.SetColumnSpan(spacer, 2);

            layout.ResumeLayout(true);
        }

        private static Control CreateControlForProperty(PropertyInfo property, object configObject)
        {
            var type = property.PropertyType;

            if (type == typeof(bool))
            {
                var checkbox = new CheckBox { Checked = (bool)property.GetValue(configObject) };
                checkbox.CheckedChanged += (s, e) => property.SetValue(configObject, checkbox.Checked);
                return checkbox;
            }
            if (type == typeof(int))
            {
                var numeric = new NumericUpDown
                {
                    Minimum = decimal.MinValue,
                    Maximum = decimal.MaxValue,
                    Value = (int)property.GetValue(configObject),
                    Width = 70
                };
                var range = property.GetCustomAttribute<RangeAttribute>();
                if (range != null)
                {
                    numeric.Minimum = (decimal)range.Minimum;
                    numeric.Maximum = (decimal)range.Maximum;
                }
                numeric.ValueChanged += (s, e) => property.SetValue(configObject, (int)numeric.Value);
                return numeric;
            }
            if (type == typeof(double))
            {
                var numeric = new NumericUpDown
                {
                    Minimum = decimal.MinValue,
                    Maximum = decimal.MaxValue,
                    DecimalPlaces = 2,
                    Increment = 0.01m,
                    Value = (decimal)(double)property.GetValue(configObject),
                    Width = 70
                };
                var range = property.GetCustomAttribute<RangeAttribute>();
                if (range != null)
                {
                    numeric.Minimum = (decimal)range.Minimum;
                    numeric.Maximum = (decimal)range.Maximum;
                }
                numeric.ValueChanged += (s, e) => property.SetValue(configObject, (double)numeric.Value);
                return numeric;
            }
            if (type.IsEnum)
            {
                var comboBox = new ComboBox
                {
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    Width = 120
                };
                comboBox.MouseWheel += (s, e) => ((HandledMouseEventArgs)e).Handled = true;
                comboBox.Items.AddRange(Enum.GetNames(type));
                comboBox.SelectedItem = property.GetValue(configObject).ToString();
                comboBox.SelectedIndexChanged += (s, e) =>
                {
                    try
                    {
                        object choice = Enum.Parse(type, comboBox.SelectedItem.ToString());
                        property.SetValue(configObject, choice);
                    }
                    catch (ArgumentException)
                    {
                        // Selected item is not a valid enum member, do nothing.
                    }
                };
                return comboBox;
            }
            if (type == typeof(Color))
            {
                var button = new Button
                {
                    Width = 100,
                    Height = 23,
                    BackColor = (Color)property.GetValue(configObject),
                    Text = property.GetCustomAttribute<ConfigPropertyAttribute>()?.DisplayName ?? property.Name
                };
                button.Click += (s, e) =>
                {
                    using (var colorDialog = new ColorDialog { Color = button.BackColor })
                    {
                        if (colorDialog.ShowDialog() == DialogResult.OK)
                        {
                            button.BackColor = colorDialog.Color;
                            property.SetValue(configObject, colorDialog.Color);
                        }
                    }
                };
                return button;
            }
            if (type == typeof(Size))
            {
                var size = (Size)property.GetValue(configObject);
                var panel = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, Padding = new Padding(0), Margin = new Padding(0) };

                var widthNumeric = new NumericUpDown
                {
                    Minimum = 0,
                    Maximum = 100000,
                    Width = 70
                };
                widthNumeric.Value = size.Width; // Set Value after Min/Max

                var heightNumeric = new NumericUpDown
                {
                    Minimum = 0,
                    Maximum = 100000,
                    Width = 70
                };
                heightNumeric.Value = size.Height; // Set Value after Min/Max

                widthNumeric.ValueChanged += (s, e) => property.SetValue(configObject, new Size((int)widthNumeric.Value, (int)heightNumeric.Value));
                heightNumeric.ValueChanged += (s, e) => property.SetValue(configObject, new Size((int)widthNumeric.Value, (int)heightNumeric.Value));

                panel.Controls.Add(new Label { Text = "W:", AutoSize = true, Margin = new Padding(0, 5, 0, 0) });
                panel.Controls.Add(widthNumeric);
                panel.Controls.Add(new Label { Text = "H:", AutoSize = true, Margin = new Padding(5, 5, 0, 0) });
                panel.Controls.Add(heightNumeric);
                return panel;
            }

            return new Label { Text = $"Unsupported type: {type.Name}" };
        }
    }
}