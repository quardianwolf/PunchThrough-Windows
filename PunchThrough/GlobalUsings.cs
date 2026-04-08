// Resolve ambiguity between System.Windows.Application (WPF) and System.Windows.Forms.Application (WinForms)
global using Application = System.Windows.Application;
