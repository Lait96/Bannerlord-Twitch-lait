## Version 1.5.3 — Formation commands and ranged combat

- Expanded formation control with positioning, ignored enemy classes, hunting enemy BLT heroes, ranged siege weapon entry/exit, and a control status command.
- Added `advance`: ranged heroes approach to firing range and reposition as enemies move.
- Fixed suppressed ranged AI priorities under the individual `charge` order.
- Added **Prioritize ranged ammunition** to the general **Battle** settings, enabled by default. Adopted AI heroes on both sides prefer usable ranged and throwing weapons while ammunition remains. Hold-fire, retreat, explicit positioning, mounting, and siege weapon orders are respected.
- Fixed returning heroes to normal formation control, including restoration of firing and riding orders and cleanup of detachment registrations.
- Improved handling of formation transfers, failed commands, missing targets and mounts, and gate/wall commands outside sieges.
- Fixed navigation to siege weapon operating positions and cleanup when leaving, changing orders, or removing an agent.
- Corrected formation positioning, target reset behavior, selection of throwing weapons with ammunition, and conflicts between individual orders and automatic enemy attacks.
- Compatibility warnings now compare only the major and minor game version: patch/build differences within the same branch no longer trigger a warning.
- Added console test scenarios and updated Russian command documentation.

This release targets Bannerlord **1.4.***. Normal battle tests were performed on **1.4.8**, including ranged firing, `advance` repositioning, and order cancellation; no crashes were observed in those tests. Siege behavior has not yet been verified in game. No separate 1.3.15 build is included.

Version **1.5.2** was a private beta and is skipped in the public release sequence.

---

## Версия 1.5.3 — команды формации и стрелковый бой

- Расширено управление формацией: расположение героя, игнорирование классов врагов, охота на вражеских БЛТ, занятие и освобождение стрелковых осадных орудий, просмотр текущих приказов.
- Добавлена команда **«сблизиться»** (`advance`): стрелок подходит на дистанцию выстрела и меняет позицию при движении противника.
- Исправлено подавление стрелкового поведения при индивидуальном приказе **«атака»** (`charge`).
- В общих настройках раздела **«Бой»** добавлен **«Приоритет расходования боезапаса»**, включённый по умолчанию. БЛТ под управлением ИИ на обеих сторонах предпочитают подходящее стрелковое и метательное оружие, пока есть боезапас. Учитываются запрет огня, отступление, индивидуальные приказы движения, посадка на коня и работа с осадными орудиями.
- Исправлен возврат под обычное управление формации: восстановление режимов стрельбы и езды, удаление регистраций отделённых героев.
- Улучшена обработка смены формации, ошибочных приказов, отсутствующих целей и коней, команд ворот и стен вне осады.
- Исправлены подход к рабочим точкам осадных орудий и освобождение управления при выходе, смене приказа или удалении персонажа.
- Исправлены расположение в формации, сброс приоритетных целей, выбор метательного оружия с боезапасом и конфликты индивидуальных приказов с автоматической атакой вражеских БЛТ.
- Предупреждение о несовместимости теперь сравнивает только первые два компонента версии игры: различия патча и номера сборки внутри одной ветки не вызывают предупреждение.
- Добавлены консольные сценарии проверки в игре и обновлена русская документация команды формации.

Релиз предназначен для Bannerlord **1.4.***. Проверки обычного боя проведены на **1.4.8**: стрельба, изменение дистанции при `advance` и отмена приказа; в этих проверках вылетов не наблюдалось. Осадное поведение ещё не проверено в игре. Отдельная сборка для 1.3.15 не включена.

Версия **1.5.2** была закрытой бетой и пропускается в последовательности публичных релизов.
