using System;
using System.Collections.Concurrent;
using System.Reflection;

namespace CMOLocalizer
{
    /// <summary>
    /// 注入进程内渲染热路径上唯一允许的反射入口（评审 finding O-02 / PRF-003）。
    ///
    /// 背景：CMOLocalizer.cs 的 UI 扫描每 2.5s 走一遍控件树，热路径上曾反复写
    /// <c>X.GetType().GetProperty(name, flags)</c>。同一个 (Type, 成员名, BindingFlags)
    /// 三元组每次都要重新解析元数据（类型层次遍历 + 成员匹配 + 访问器绑定），
    /// 单次开销不大，但在每轮上千次调用下是纯浪费。本类把这层解析收敛成一次，
    /// 之后只做一次字典查找。
    ///
    /// 使用约定：
    /// * 只允许在注入进程（本 DLL 内部，namespace CMOLocalizer）使用；不要把宿主
    ///   进程或外部工具的类型塞进来——缓存是进程级静态的，键里带着宿主 Type 引用。
    /// * 查不到就返回 null（未命中的类型/名字也返回 null），调用点原有的 null 判定
    ///   一律不得改动。
    /// * 本类不制造任何新异常，也不吞掉底层反射调用原本会抛出的异常：
    ///   AmbiguousMatchException（同名成员隐藏）等仍按原样向调用者传播，
    ///   与直接调用 <c>Type.GetProperty</c> 的异常行为逐字一致。例如
    ///   TranslateWpfElement 里那次 Document 解析位于 try 内，异常会中止整轮
    ///   WPF 遍历；若在这里把异常改成 null，后续的窗口标题翻译与子树递归就会被
    ///   悄悄放行——那是行为变更，不是优化。
    /// * <b>null 结果同样入缓存</b>：原生控件没有 Document / WatermarkText 是热路径上
    ///   最常见的分支，不缓存 null 等于没优化（每次仍要付一次元数据解析）。
    ///
    /// 关于键类型：任务书上写的 <c>ConcurrentDictionary&lt;(Type,string,BindingFlags), PropertyInfo&gt;</c>
    /// 在 C# 5 里不可编译——本工程用 Framework64/v4.0.30319/csc.exe（仅支持 C# 5），
    /// 没有 ValueTuple。这里用等价的只读结构体键：语义相同（按 Type/名称/Flags 三元组
    /// 相等），且比 System.Tuple 更省——值类型键 + IEquatable&lt;T&gt;，查询不装箱、
    /// 不分配。
    /// </summary>
    public static class Reflect
    {
        // 进程级缓存：反射成员解析结果（含 null 未命中）。键的比较见 MemberKey。
        private static readonly ConcurrentDictionary<MemberKey, PropertyInfo> propertyCache =
            new ConcurrentDictionary<MemberKey, PropertyInfo>();

        /// <summary>
        /// 等价于 <c>t.GetProperty(name, flags)</c>，但结果按 (Type, 名称, flags) 缓存。
        /// 未命中返回 null（并缓存该 null）；t / name 为 null 时返回 null。
        /// </summary>
        public static PropertyInfo Prop(Type t, string name, BindingFlags flags)
        {
            if (t == null || name == null) return null;

            MemberKey key = new MemberKey(t, name, flags);
            PropertyInfo cached;
            if (propertyCache.TryGetValue(key, out cached)) return cached;

            // 与直调 Type.GetProperty 行为一致：异常（如有）向上传播，不在这里吞。
            PropertyInfo found = t.GetProperty(name, flags);

            // 并发下可能有多个线程同时解析同一键：值必然相同，先写入者胜，无需加锁。
            propertyCache.TryAdd(key, found);
            return found;
        }

        /// <summary>(Type, 成员名, BindingFlags) 三元组缓存键，值语义相等。</summary>
        private struct MemberKey : IEquatable<MemberKey>
        {
            private readonly Type type;
            private readonly string name;
            private readonly BindingFlags flags;

            public MemberKey(Type type, string name, BindingFlags flags)
            {
                this.type = type;
                this.name = name;
                this.flags = flags;
            }

            public bool Equals(MemberKey other)
            {
                return object.Equals(type, other.type)
                    && flags == other.flags
                    && string.Equals(name, other.name, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is MemberKey && Equals((MemberKey)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int h = flags.GetHashCode();
                    h = (h * 397) ^ (type != null ? type.GetHashCode() : 0);
                    h = (h * 397) ^ (name != null ? name.GetHashCode() : 0);
                    return h;
                }
            }
        }
    }
}
