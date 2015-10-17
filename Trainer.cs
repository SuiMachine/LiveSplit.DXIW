/* C# trainer class.
 * Author : Cless
 */

using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Runtime.InteropServices;


public class Trainer
{
    private const int PROCESS_ALL_ACCESS = 0x1F0FFF;
    [DllImport("kernel32")]
    private static extern int OpenProcess(int AccessType, int InheritHandle, int ProcessId);


    [DllImport("kernel32", EntryPoint = "ReadProcessMemory")]
    private static extern byte ReadProcessMemoryByte(int Handle, int Address, ref byte Value, int Size, ref int BytesRead);


    [DllImport("kernel32")]
    private static extern int CloseHandle(int Handle);

    [DllImport("user32")]
    private static extern int FindWindow(string sClassName, string sAppName);
    [DllImport("user32")]
    private static extern int GetWindowThreadProcessId(int HWND, out int processId);


    public static string CheckGame(string WindowTitle)
    {
        string result = "";
        checked
        {
            try
            {
                int Proc;
                int HWND = FindWindow(null, WindowTitle);
                GetWindowThreadProcessId(HWND, out Proc);
                int Handle = OpenProcess(PROCESS_ALL_ACCESS, 0, Proc);
                if (Handle != 0)
                {
                    result = "Game is running...";
                }
                else
                {
                    result = "Game is not running...";
                }
                CloseHandle(Handle);
            }
            catch
            { }
        }
        return result;
    }

    public static byte ReadByte(Process Proc, int Address)
    {
        byte Value = 0;
        checked
        {
            try
            {
                if (Proc != null)
                {
                    int Bytes = 0;
                    int Handle = OpenProcess(PROCESS_ALL_ACCESS, 0, Proc.Id);
                    if (Handle != 0)
                    {
                        ReadProcessMemoryByte(Handle, Address, ref Value, 2, ref Bytes);
                        CloseHandle(Handle);
                    }
                }
            }
            catch
            { }
        }
        return Value;
    }
}