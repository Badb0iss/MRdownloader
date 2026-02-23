using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using System.Diagnostics;

namespace MRdownload
{
    // Ahora heredamos de FluentWindow para activar las funciones de WPF-UI
    public partial class MainWindow : Wpf.Ui.Controls.FluentWindow
    {
        private static readonly HttpClient clienteHttp = new HttpClient();
        
        // Variable para guardar la ruta si el usuario decide elegir una
        private string carpetaDestinoOpcional = string.Empty;

        public MainWindow()
        {
            InitializeComponent();
            // Esto asegura que la app respete si Windows está en modo oscuro o claro
            Wpf.Ui.Appearance.SystemThemeWatcher.Watch(this);
        }

        // Lógica para elegir una carpeta personalizada
        private void BtnSeleccionarCarpeta_Click(object sender, RoutedEventArgs e)
        {
            OpenFolderDialog folderDialog = new OpenFolderDialog
            {
                Title = "Selecciona la carpeta donde se guardarán los mods"
            };

            if (folderDialog.ShowDialog() == true)
            {
                carpetaDestinoOpcional = folderDialog.FolderName;
                TxtCarpeta.Text = $"Destino: {carpetaDestinoOpcional}";
            }
        }

        private async void BtnBuscar_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Modrinth Pack (*.mrpack)|*.mrpack|Todos los archivos (*.*)|*.*",
                Title = "Selecciona tu archivo .mrpack"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                BtnBuscar.IsEnabled = false;
                await ExtraerYDescargar(openFileDialog.FileName);
                BtnBuscar.IsEnabled = true;
            }
        }

        private async Task ExtraerYDescargar(string rutaMrpack)
        {
            // Verificamos si el usuario eligió una carpeta. Si no, usamos la ruta automática.
            string carpetaSalida = string.IsNullOrEmpty(carpetaDestinoOpcional) 
                                   ? rutaMrpack.Replace(".mrpack", "_mods") 
                                   : carpetaDestinoOpcional;

            Directory.CreateDirectory(carpetaSalida);

            try
            {
                using ZipArchive archivoZip = ZipFile.OpenRead(rutaMrpack);
                ZipArchiveEntry indexEntry = archivoZip.GetEntry("modrinth.index.json");

                if (indexEntry == null) throw new Exception("No se encontró modrinth.index.json en este archivo.");

                using Stream streamIndex = indexEntry.Open();
                using StreamReader reader = new StreamReader(streamIndex);
                string jsonContenido = await reader.ReadToEndAsync();

                using JsonDocument doc = JsonDocument.Parse(jsonContenido);
                JsonElement arrayArchivos = doc.RootElement.GetProperty("files");
                
                int totalMods = arrayArchivos.GetArrayLength();
                BarraProgreso.Maximum = totalMods;
                BarraProgreso.Value = 0;

                int descargados = 0;

                foreach (JsonElement mod in arrayArchivos.EnumerateArray())
                {
                    string url = mod.GetProperty("downloads")[0].GetString();
                    string pathInterno = mod.GetProperty("path").GetString();
                    string nombreArchivo = Path.GetFileName(pathInterno);
                    string rutaFinal = Path.Combine(carpetaSalida, nombreArchivo);

                    TxtEstado.Text = $"Descargando ({descargados + 1}/{totalMods}): {nombreArchivo}";

                    bool exito = false;
                    for (int intento = 0; intento < 3; intento++)
                    {
                        try
                        {
                            byte[] data = await clienteHttp.GetByteArrayAsync(url);
                            await File.WriteAllBytesAsync(rutaFinal, data);
                            exito = true;
                            break; 
                        }
                        catch
                        {
                            TxtEstado.Text = $"Fallo al descargar {nombreArchivo}. Reintento {intento + 1}/3...";
                            await Task.Delay(2000); 
                        }
                    }

                    if (!exito) MessageBox.Show($"No se pudo descargar {nombreArchivo}", "Error de red");

                    descargados++;
                    BarraProgreso.Value = descargados;
                }

                TxtEstado.Text = "¡Descarga completada con éxito!";
                
                // Abre la carpeta de destino automáticamente
                Process.Start("explorer.exe", carpetaSalida); 
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ocurrió un error:\n{ex.Message}", "Error");
                TxtEstado.Text = "Error en la extracción.";
            }
        }
    }
}