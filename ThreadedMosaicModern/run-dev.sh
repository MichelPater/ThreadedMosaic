#!/bin/bash
# ThreadedMosaic Development Environment Startup Script
# Usage: ./run-dev.sh [local|docker] [--clean]

set -e

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'  
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Default values
MODE="${1:-local}"
CLEAN="${2}"

print_color() {
    local color=$1
    local message=$2
    echo -e "${color}${message}${NC}"
}

print_header() {
    print_color $BLUE "
╔══════════════════════════════════════════════════════════════════════╗
║                    ThreadedMosaic Development                        ║
║                         Startup Script                              ║
╚══════════════════════════════════════════════════════════════════════╝
"
}

check_dotnet() {
    if command -v dotnet &> /dev/null; then
        local version=$(dotnet --version)
        print_color $GREEN "✓ .NET $version installed"
        return 0
    else
        print_color $RED "✗ .NET SDK not found. Please install .NET 9 SDK"
        return 1
    fi
}

check_docker() {
    if command -v docker &> /dev/null; then
        local version=$(docker --version)
        print_color $GREEN "✓ $version installed"
        return 0
    else
        print_color $RED "✗ Docker not found. Please install Docker"
        return 1
    fi
}

cleanup_ports() {
    print_color $YELLOW "🔄 Stopping existing services on ports 5001, 7001, 5002, 7002..."
    
    # Kill processes on our ports (Linux/macOS)
    for port in 5001 7001 5002 7002; do
        local pid=$(lsof -ti:$port 2>/dev/null || true)
        if [ ! -z "$pid" ]; then
            kill -9 $pid 2>/dev/null || true
            print_color $YELLOW "Stopped process $pid using port $port"
        fi
    done
}

start_local() {
    print_color $BLUE "🚀 Starting ThreadedMosaic in LOCAL mode..."
    
    if ! check_dotnet; then
        exit 1
    fi
    
    if [ "$CLEAN" = "--clean" ]; then
        print_color $YELLOW "🧹 Cleaning projects..."
        dotnet clean ThreadedMosaic.sln
        dotnet build ThreadedMosaic.sln --configuration Debug
    fi
    
    cleanup_ports
    
    print_color $GREEN "📡 Starting API Server on https://localhost:7001 and http://localhost:5001..."
    dotnet run --project "ThreadedMosaic.Api/ThreadedMosaic.Api.csproj" --urls "https://localhost:7001;http://localhost:5001" &
    API_PID=$!
    
    # Wait for API to start
    sleep 3
    
    print_color $GREEN "🌐 Starting Blazor Server on https://localhost:7002 and http://localhost:5002..."
    dotnet run --project "ThreadedMosaic.BlazorServer/ThreadedMosaic.BlazorServer.csproj" --urls "https://localhost:7002;http://localhost:5002" &
    BLAZOR_PID=$!
    
    print_color $BLUE "⏳ Waiting for services to start..."
    sleep 5
    
    # Test API health
    if curl -f http://localhost:5001/health &>/dev/null; then
        print_color $GREEN "✓ API service is healthy"
    else
        print_color $YELLOW "⚠ API service may still be starting..."
    fi
    
    # Test Blazor health
    if curl -f http://localhost:5002 &>/dev/null; then
        print_color $GREEN "✓ Blazor service is healthy"
    else
        print_color $YELLOW "⚠ Blazor service may still be starting..."
    fi
    
    print_color $GREEN "
🎉 ThreadedMosaic Development Environment Started!

📡 API Server:     https://localhost:7001 (primary) | http://localhost:5001
🌐 Blazor Server:  https://localhost:7002 (primary) | http://localhost:5002  
📚 API Docs:       https://localhost:7001/swagger

Press Ctrl+C to stop all services...
"
    
    # Trap cleanup
    trap cleanup_and_exit INT
    
    cleanup_and_exit() {
        print_color $YELLOW "🛑 Stopping services..."
        kill $API_PID $BLAZOR_PID 2>/dev/null || true
        wait $API_PID $BLAZOR_PID 2>/dev/null || true
        print_color $GREEN "✓ All services stopped"
        exit 0
    }
    
    # Wait for interrupt
    wait
}

start_docker() {
    print_color $BLUE "🐳 Starting ThreadedMosaic in DOCKER mode..."
    
    if ! check_docker; then
        exit 1
    fi
    
    if [ "$CLEAN" = "--clean" ]; then
        print_color $YELLOW "🧹 Cleaning Docker containers and images..."
        docker-compose down --rmi all --volumes --remove-orphans
    fi
    
    print_color $GREEN "📦 Building and starting Docker containers..."
    docker-compose up --build -d
    
    print_color $BLUE "⏳ Waiting for services to start..."
    sleep 10
    
    print_color $GREEN "
🎉 ThreadedMosaic Docker Environment Started!

📡 API Server:     https://localhost:7001 | http://localhost:5001
🌐 Blazor Server:  https://localhost:7002 | http://localhost:5002
📚 API Docs:       https://localhost:7001/swagger

View logs: docker-compose logs -f
Stop:      docker-compose down
"
}

show_usage() {
    print_color $YELLOW "
Usage: ./run-dev.sh [MODE] [OPTIONS]

MODES:
  local   - Run with dotnet run (default)
  docker  - Run with docker-compose

OPTIONS:
  --clean - Clean build before starting

EXAMPLES:
  ./run-dev.sh local
  ./run-dev.sh docker --clean
"
}

# Main execution
print_header

case "$MODE" in
    "local")
        start_local
        ;;
    "docker") 
        start_docker
        ;;
    "--help"|"-h")
        show_usage
        ;;
    *)
        print_color $RED "Invalid mode: $MODE"
        show_usage
        exit 1
        ;;
esac