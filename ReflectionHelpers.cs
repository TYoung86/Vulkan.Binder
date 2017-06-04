using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Artilect.Vulkan.Binder {
	public static class ReflectionHelpers {
		
		public static object ResolveConst(MemberBinding binding) {
			if (binding is MemberAssignment assignment)
				return ResolveConst(assignment.Expression);
			if (binding is MemberListBinding list)
				return list.Initializers.Select(init =>
						init.Arguments.Select(ResolveConst))
					.ToArray();
			if (binding is MemberMemberBinding deeperBinding)
				throw new NotImplementedException();
			throw new NotImplementedException();
		}

		public static object ResolveConst(Expression expr) {
			if (expr == null) return null;

			while (expr.CanReduce) expr = expr.Reduce();

			if (expr is ConstantExpression constExpr)
				return constExpr.Value;

			if (expr is MemberExpression memberExpr) {
				var src = ResolveConst(memberExpr.Expression);
				if (memberExpr.Member is FieldInfo fi)
					return fi.GetValue(src);
				if (memberExpr.Member is PropertyInfo pi)
					return pi.GetValue(src);
				throw new NotImplementedException();
			}

			if (expr is MethodCallExpression callExpr) {
				var src = ResolveConst(callExpr.Object);
				var args = callExpr.Arguments.Select(ResolveConst).ToArray();
				return callExpr.Method.Invoke(src, args);
			}

			if (expr is UnaryExpression unaryExpr) {
				if (unaryExpr.NodeType == ExpressionType.Convert) {
					var value = ResolveConst(unaryExpr.Operand);
					return Convert.ChangeType(value, unaryExpr.Type);
				}
				throw new NotImplementedException();
			}

			throw new NotImplementedException();
		}

	}
}