output "function_name" {
  description = "Nome da função Lambda"
  value       = aws_lambda_function.auth.function_name
}

output "function_arn" {
  description = "ARN da função Lambda (usado pela integração do API Gateway no CARD-30)"
  value       = aws_lambda_function.auth.arn
}

output "function_invoke_arn" {
  description = "Invoke ARN — formato exigido pela integração AWS_PROXY do API Gateway"
  value       = aws_lambda_function.auth.invoke_arn
}
