using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace MSG
{
    public struct m_s_g             // Paket
    {
        // Absender ist unbekannt
        public short task;          // Aufgabe
        public string text;         // Text
        public m_s_g(short tsk, string txt)
        {
            task = tsk;
            text = txt;
        }
    };
    public class Msg        // Postfächer-Klasse
    {
        short IDs = 0;      // Aktuelle angemeldete Zahl an Threads
        public Msg() { }

        public List<Queue<m_s_g>> postfach = new List<Queue<m_s_g>>(); // Postfächer

        public short register()         // Neuen Thread hinzufügen
        {
            postfach.Add(new Queue<m_s_g>()); // Neues Postfach
            short old = IDs;            // IDs starten bei 0
            IDs++;
            return old;
        }

        public void send(short target, short task, string text) // Paket senden // 'target' = Empfänger
        {
            m_s_g msg = new m_s_g(task, text);   // Paket erstellen 
            postfach[target].Enqueue(msg);          // In Postfach legen
        }

        public m_s_g? receive(short id)             // Paket empfangen
        {
            if (postfach[id].Count > 0)             // Wenn keine Pakete vorhanden: Einfacher Check; Anderndfalls Paket aus Postfach holen
            {
                return postfach[id].Dequeue();      // Paket aus Postfach holen
            }
            return null;
        }
    }
}