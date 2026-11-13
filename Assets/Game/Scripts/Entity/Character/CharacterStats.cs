using System;
using System.Collections.Generic;
using UnityEngine;

namespace SketchLand
{
    public class CharacterStats
    {
        public Action<Buff> OnBuffStarted;
        public Action<Buff> OnBuffStopped;

        public Stat MoveSpeed { get; }
        public Stat MaxHealth { get; }
        public Stat Defense { get; }

        private readonly List<Buff> _activeBuffs = new();
        public IReadOnlyList<Buff> ActiveBuffs => _activeBuffs;

        public CharacterStats(CharacterSO data)
        {
            MoveSpeed = new(data.BaseSpeed);
            MaxHealth = new(data.BaseMaxHealth);
            Defense = new(data.BaseDefense);
        }

        public void TickBuffs(float deltaTime)
        {
            for (int i = _activeBuffs.Count - 1; i >= 0; i--)
            {
                var buff = _activeBuffs[i];
                buff.Tick(deltaTime, this);
                if (!buff.IsActive)
                {
                    _activeBuffs.RemoveAt(i);
                    OnBuffStopped?.Invoke(buff);
                }
            }
        }

        public void StartBuff(Buff buff)
        {
            if (buff == null) return;
            if (_activeBuffs.Contains(buff)) return;

            _activeBuffs.Add(buff);
            buff.ApplyTo(this);
            Debug.Log($"Added buff {buff.Name}");
            OnBuffStarted?.Invoke(buff);
        }

        public void StopBuff(Buff buff)
        {
            if (buff == null) return;
            if (!_activeBuffs.Remove(buff)) return;

            buff.Stop(this);
            Debug.Log($"Stop buff {buff.Name}");
            OnBuffStopped?.Invoke(buff);
        }

        public bool HasBuff(string buffName)
        {
            for (int i = 0; i < _activeBuffs.Count; i++)
            {
                if (_activeBuffs[i].Name.Equals(buffName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            
            return false;
        }

        public Stat GetStat(StatType type)
        {
            return type switch
            {
                StatType.MoveSpeed => MoveSpeed,
                StatType.MaxHealth => MaxHealth,
                StatType.Defense => Defense,
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };
        }
    }
}