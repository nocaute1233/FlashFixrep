# Fase 3 — hardware e Visão Geral

## Fontes de dados

- WMI: CPU, GPU, Windows, tipo de máquina, RAM, discos, tipo SSD/HDD e display.
- Interfaces de rede do .NET: Ethernet ou Wi-Fi ativo.
- `GetSystemTimes`: amostra de 500 ms para utilização real da CPU.
- NVML do driver NVIDIA: utilização da GPU quando instalada e suportada.

A página mostra ainda percentagem de RAM ocupada e espaço usado nos volumes
fixos. A atualização é manual para evitar consumo contínuo em idle; cada
leitura ocorre fora da thread da interface.

Em hardware AMD/Intel ou quando a telemetria NVIDIA falha, a utilização da GPU
aparece como indisponível. A percentagem de disco indica capacidade ocupada,
não atividade de I/O. O tipo SSD/HDD é agregado dos discos físicos e não é
atribuído a um volume específico quando há vários discos.

## Verificação efetuada

No PC de desenvolvimento, a aplicação identificou Windows 10, desktop,
Intel Core i5-11400F, GeForce RTX 3050, 15,9 GB de RAM, SSD, Ethernet e
display 1920×1080 a 164 Hz. A amostra de CPU devolveu 8–9% e a NVML devolveu
0% no momento do teste. O botão de atualização alterou o horário da amostra.
Toda a solução compilou sem erros ou avisos.
