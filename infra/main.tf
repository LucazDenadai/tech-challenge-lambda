# IAM role da Lambda — permissões mínimas: assumir a role e escrever logs no CloudWatch.
# Acesso à VPC (ENI) é dado pela policy gerenciada AWSLambdaVPCAccessExecutionRole.
data "aws_iam_policy_document" "lambda_assume_role" {
  statement {
    actions = ["sts:AssumeRole"]
    effect  = "Allow"

    principals {
      type        = "Service"
      identifiers = ["lambda.amazonaws.com"]
    }
  }
}

resource "aws_iam_role" "lambda" {
  name               = "${var.function_name}-role"
  assume_role_policy = data.aws_iam_policy_document.lambda_assume_role.json
}

resource "aws_iam_role_policy_attachment" "lambda_vpc_access" {
  role       = aws_iam_role.lambda.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AWSLambdaVPCAccessExecutionRole"
}

# Security group da Lambda — sem regras de ingress (a Lambda não recebe conexões
# de entrada, só é invocada via API Gateway/AWS API). Egress liberado para conseguir
# conectar ao RDS.
resource "aws_security_group" "lambda" {
  name        = "${var.function_name}-sg"
  description = "Security group da Lambda de autenticacao"
  vpc_id      = data.terraform_remote_state.infra_k8s.outputs.vpc_id

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }
}

# Libera o RDS para aceitar conexões da Lambda — o security group do RDS já existe no
# tech-challenge-infra-db; esta regra é gerenciada aqui (não lá) para não criar
# dependência circular entre os dois repositórios de infra.
resource "aws_security_group_rule" "rds_from_lambda" {
  type                     = "ingress"
  from_port                = 5432
  to_port                  = 5432
  protocol                 = "tcp"
  security_group_id        = data.terraform_remote_state.infra_db.outputs.rds_security_group_id
  source_security_group_id = aws_security_group.lambda.id
  description              = "PostgreSQL a partir da Lambda de autenticacao"
}

data "terraform_remote_state" "infra_db" {
  backend = "s3"

  config = {
    bucket = "tech-challenga-tfstate"
    key    = "infra-db/terraform.tfstate"
    region = "us-east-1"
  }
}

resource "aws_lambda_function" "auth" {
  function_name = var.function_name
  role          = aws_iam_role.lambda.arn
  handler       = "OficinaMecanica.Auth.Lambda::OficinaMecanica.Auth.Lambda.Function::FunctionHandler"
  runtime       = "dotnet8"
  timeout       = 30
  memory_size   = 512

  filename         = "${path.module}/../artifacts/lambda.zip"
  source_code_hash = filebase64sha256("${path.module}/../artifacts/lambda.zip")

  vpc_config {
    subnet_ids         = data.terraform_remote_state.infra_k8s.outputs.private_subnet_ids
    security_group_ids = [aws_security_group.lambda.id]
  }

  environment {
    variables = {
      DB_CONNECTION_STRING = var.db_connection_string
      JWT_KEY              = var.jwt_key
      JWT_ISSUER           = var.jwt_issuer
      JWT_AUDIENCE         = var.jwt_audience
    }
  }
}
