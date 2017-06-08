using System;
using System.Collections.Generic;

namespace Artilect.Vulkan.Binder.Extensions {
	public static class CollectionExtensions {

		public static IEnumerable<T> Reversed<T>(this LinkedList<T> linkedList) {
			var node = linkedList.Last;
			while (node != null) {
				yield return node.Value;
				node = node.Previous;
			}
		}
		
		public static IEnumerable<LinkedListNode<T>> Nodes<T>(this LinkedList<T> linkedList) {
			var node = linkedList.First;
			while (node != null) {
				yield return node;
				node = node.Next;
			}
		}

		public static void ConsumeLinkedList<T>(this LinkedList<T> linkedList, Action<T, int> action) {
			int i = 0;
			var node = linkedList.First;
			while (node != null) {
				action(node.Value, i++);
				linkedList.RemoveFirst();
				node = linkedList.First;
			}
		}

		public static void ConsumeLinkedList<T>(this LinkedList<T> linkedList, Action<T> action) {
			var node = linkedList.First;
			while (node != null) {
				action(node.Value);
				linkedList.RemoveFirst();
				node = linkedList.First;
			}
		}

		public static void ConsumeLinkedListInReverse<T>(this LinkedList<T> linkedList, Action<T, int> action) {
			int i = 0;
			var node = linkedList.Last;
			while (node != null) {
				action(node.Value, i++);
				linkedList.RemoveLast();
				node = linkedList.Last;
			}
		}

		public static void ConsumeLinkedListInReverse<T>(this LinkedList<T> linkedList, Action<T> action) {
			var node = linkedList.Last;
			while (node != null) {
				action(node.Value);
				linkedList.RemoveLast();
				node = linkedList.Last;
			}
		}
	}
}