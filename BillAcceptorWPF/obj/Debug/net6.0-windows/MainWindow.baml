   M S B A M L   `   `   `  ����  BillAcceptorWPF     BillAcceptorWPF.MainWindow   5   	   W SPresentationCore, Version=6.0.2.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35\ XPresentationFramework, Version=6.0.2.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35e aSystem.Windows.Controls.Ribbon, Version=6.0.2.0, Culture=neutral, PublicKeyToken=b77a5c561934e089R NWindowsBase, Version=6.0.2.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35F 9http://schemas.microsoft.com/winfx/2006/xaml/presentation     5   	   R NSystem.Xaml, Version=6.0.2.0, Culture=neutral, PublicKeyToken=b77a5c561934e089:x,http://schemas.microsoft.com/winfx/2006/xaml     5   	     � Title$  Bill Acceptor Controller��5   	   $	��450��6*   $	��800��67   .��5      �  � RowDefinitions 5   
   �� 5      $
��Auto �6   �� 5	      $��* �6   �� 5
      $
��Auto �6   .+�5   
   �� $��0��6   $��10q�6"   .+�5      �� $%��Puerto del Aceptador (TBV-100):��6   $��0,0,0,5q�6?   �� 5       �� Orientation$ 
Horizontal=�6   $��0,0,0,10q�62   .+�5      �� -   5         ��Name$ ComPortComboBox��5      $	��150��64   $��0,0,10,0q�6@   �� 5      -   5         ��Name$ RefreshButton��5      $	��↻��60   $��30��6<    � ToolTip$  Refrescar lista de puertos��6c   5      �� 5      $ 
Horizontal=�6   $��0,0,0,10q�62   .+�5      �� -   5        $ ConnectButton��5      $��Conectar��60   $	��100��6C   $��0,0,10,0q�6O   6a   �� 5      -   5        $ DisconnectButton��5      $��Desconectar��63   $	��100��6I   ${�False��6U   6g   5      �� 5      $ 
Horizontal=�6   .+�5      �� -   5        $ InitializeButton��5      $��Inicializar Periféricos��63   ${�False��6t   $��0,0,10,0q�6�   �� 5      -   5        $ StartButton��5      $��OR:START��6.   $	��100��6A   $��0,0,10,0q�6M   ${�False��6y   �� 5      -   5        $ 
StopButton��5      $��	OFF:OR:AP��6-   $	��100��6A   ${�False��6f   5      5      � 5    
   $��1��6   $��10q�6    � ColumnDefinitions 5!      �� 5"      $��* �6#   �� 5#      $��* �6#   .+�5'      �� $��Status��6   $��0��6'   $��0,0,5,0q�67   .��5(      �� .+�5)      �� $��Current State:��6    $	��0,5q�66   �� 5*      -   5         ��Name$ StateTextBlock��5*       $��Disconnected��68   $	��5,0q�6L   �� 5+      $��Last Bill Amount:��6    $��0,10,0,5q�69   �� 5,      -	   5        $ BillAmountTextBlock��5,       $��0��6=   $	��5,0q�6F   5-      5.      �� 51      $	��Log��6   $��1��6$   $��5,0,0,0q�64   .��52      �� -
   5         ��Name$ 
LogTextBox��52      	 �� 
IsReadOnly$
	 True��6.   
 �� TextWrapping$

 Wrap=�6@   " �� VerticalScrollBarVisibility$
 Auto=�6T   53      54      �� 57   
   $��2��6   .@�58      �� .��59      �� -   5        $ StatusBarTextBlock��59      $��Ready��68   5:      5;      5<      5=      