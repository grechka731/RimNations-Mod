# 👑 RimNations: Grand Strategy Mod (Source Code)
### Полные исходные коды C# модификации RimNations для RimWorld 1.6

[![Engine](https://img.shields.io/badge/RimWorld-1.6-brightgreen.svg)](https://rimworldgame.com/)
[![Runtime](https://img.shields.io/badge/Platform-.NET%20Framework%204.7.2-blue.svg)](https://dotnet.microsoft.com/)
[![Built By](https://img.shields.io/badge/Code-100%25%20Vibecode%20(Google%20Antigravity%20AI)-orange.svg)](#важное-уведомление--это-всё-вайбкод-)
[![Release](https://img.shields.io/github/v/release/grechka731/RimNations-Mod?color=blue&label=Mod%20Release)](https://github.com/grechka731/RimNations-Mod/releases/latest)
[![Download](https://img.shields.io/badge/Download-RimNationsMod.zip-brightgreen.svg)](https://github.com/grechka731/RimNations-Mod/releases/download/v1.0.0/RimNationsMod.zip)

---

### 📦 Быстрая установка готового мода (Без компиляции)
> Чтобы просто играть в мод, не нужно ничего компилировать!
> Перейдите в раздел **[Releases](https://github.com/grechka731/RimNations-Mod/releases/latest)** и скачайте **[`RimNationsMod.zip`](https://github.com/grechka731/RimNations-Mod/releases/download/v1.0.0/RimNationsMod.zip)**.
> Распакуйте архив в папку `RimWorld/Mods/` и включите мод в игре.

---

## ⚡ ВАЖНОЕ УВЕДОМЛЕНИЕ: ЭТО ВСЁ ВАЙБКОД! / THIS IS ALL VIBECODE!

### [Русский]
> **💥 ВНИМАНИЕ: ЭТОТ РЕПОЗИТОРИЙ — 100% ЧИСТЫЙ ВАЙБКОД!**  
> Вся кодовая база этой модификации: 3D-рендер границ на глобусе планеты на GPU, двухрежимный движок тиков (FocusMap/GovernorMap), система конституционных министерств с разделением полномочий, глобальная товарная биржа (Order Book), интерактивные КПП, ЧВК, система государственных займов под залог городов и математические алгоритмы уникальных цветов наций были **полностью написаны нейросетью (Google Antigravity AI)** в режиме интерактивного вайбкодинга!  
> Здесь нет ни строчки кода, написанной человеком-программистом. Код компилируется без единой ошибки (0 ошибок) и строго следует стандартам Verse/RimWorld 1.6 API. Тем не менее, это чистый ИИ-вайбкод — в нем могут встретиться интересные граничные эффекты или неожиданные нюансы механик. Исследуйте код, экспериментируйте и присылайте пуллреквесты!

### [English]
> **💥 ATTENTION: THIS REPOSITORY IS 100% PURE VIBECODE!**  
> This entire codebase: 3D spherical world boundary mesh rendering on GPU, dual-state tick throttling engine, constitutional ministry system, global commodities order book, interactive border checkpoints, PMCs, collateralized sovereign debt, and deterministic distinct color distribution algorithms were **authored and generated entirely by an Artificial Intelligence / Neural Network (Google Antigravity AI)** via end-to-end prompt-driven vibecoding!  
> Not a single line was written by a human software developer. It compiles cleanly with 0 errors. As genuine AI vibecode, it may contain edge-case quirks. Inspect the code, experiment, and submit PRs!

---

## 📁 Структура исходного кода

- `Source/Client/GrandStrategy/` — Ядро глобальной стратегии:
  - `UI/WorldLayer_NationBorders.cs` — 3D GPU-меш рендерер границ на планете, алгоритм органического захвата территорий (10–13 клеток для столицы, 7–10 для городов), система независимых цветовых батчей.
  - `UI/Dialog_RimNationsDashboard.cs` — Главная панель державы (горячая клавиша **U**), разделы геральдики, министерств, дипломатии, биржи и займов.
  - `Core/WorldComponent_StateGovernor.cs` — Высококонтрастная 16-якорная алгоритмическая генерация цветов по логину, управление казной, ЧВК и займами.
  - `Core/GovernmentModels.cs` — Модели данных министерств, контрактов, долговых обязательств и разрешений.
  - `Harmony/Patch_CaravanTransitTax.cs` — Интерактивный перехват караванов на суверенных границах.
  - `Harmony/Patch_ZeroOverheadVassalMap.cs` & `Patch_MapTicking.cs` — Двухрежимный движок тиков (60 TPS FocusMap vs 8x throttled GovernorMap).
- `Source/Client/UnionDll/` — Сетевые протоколы, сериализаторы пакетов `ModelPlayToServer` / `ModelPlayToClient`.
- `Source/Client.sln` — Готовое решение для сборки через Visual Studio 2022 / `dotnet build`.

---

## 🛠 Сборка из исходников

```bash
cd Source
dotnet build Client.sln -c Release -p:RimWorldFolder="C:\Путь\К\RimWorld\\"
```
Скомпилированная библиотека автоматически выводится в `BuildOutput/Client/Release/RimWorldOnlineCity.dll`.

---

## 📜 Credits
- Сетевая архитектура вдохновлена проектом **OnlineCity** (kfish).
- Вся геополитика, механики суверенных наций и 100% вайбкода созданы **Google Antigravity AI**.
